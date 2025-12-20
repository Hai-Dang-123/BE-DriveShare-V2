using BLL.Services.Interface;
using Common.DTOs;
using Common.Enums.Type;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Common.Enums.Status;

namespace BLL.Services.Impletement
{
    public class Adminservices : IAdminservices
    {
        private readonly IUnitOfWork _unitOfWork;
        public Adminservices(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> GetOverview()
        {
            var users = _unitOfWork.BaseUserRepo.GetAll();
            var trips = _unitOfWork.TripRepo.GetAll();
            var packages = _unitOfWork.PackageRepo.GetAll();

            // 🔑 LẤY VÍ ADMIN (TIỀN NỀN TẢNG)
            var adminWallet = await _unitOfWork.WalletRepo.GetAll()
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.User.Role.RoleName == "ADMIN");

            var data = new DashboardOverviewDTO
            {
                TotalUsers = users.Count(),
                TotalDrivers = users.Count(x => x.Role.RoleName == "DRIVER"),
                TotalOwners = users.Count(x => x.Role.RoleName == "OWNER"),
                TotalProviders = users.Count(x => x.Role.RoleName == "PROVIDER"),
                TotalTrips = trips.Count(),
                TotalPackages = packages.Count(),

                // ✅ THEO LOGIC CỦA BẠN
                TotalRevenue = adminWallet?.Balance ?? 0
            };

            return new ResponseDTO("Overview loaded", 200, true, data);
        }



        public async Task<ResponseDTO> GetPackageCreatedStats(
    DateTime from,
    DateTime to,
    string groupBy)
        {
            var packages = _unitOfWork.PackageRepo.GetAll()
                .Where(x => x.CreatedAt >= from && x.CreatedAt <= to);

            var data = BuildTimeSeries(packages.Select(x => x.CreatedAt), groupBy);

            return new ResponseDTO("Package created over time", 200, true, data);
        }

        public async Task<ResponseDTO> GetPackageStatsByStatus()
        {
            var packages = _unitOfWork.PackageRepo.GetAll();

            var data = packages
                .GroupBy(x => x.Status)
                .Select(g => new StatusStatisticDTO
                {
                    Status = g.Key.ToString(),
                    Count = g.Count()
                })
                .ToList();

            return new ResponseDTO("Package by status", 200, true, data);
        }
        public async Task<ResponseDTO> GetRevenueStats(
    DateTime from,
    DateTime to,
    string groupBy)
        {
            var revenueTypes = new[]
            {
        TransactionType.DRIVER_SERVICE_PAYMENT
        // Sau này thêm:
        // TransactionType.PLATFORM_FEE,
        // TransactionType.SURCHARGE_PAYMENT
    };

            var baseQuery = _unitOfWork.TransactionRepo.GetAll()
                .Where(t =>
                    t.CreatedAt >= from &&
                    t.CreatedAt <= to &&
                    t.Status == TransactionStatus.SUCCEEDED &&
                    t.Amount > 0 &&
                    revenueTypes.Contains(t.Type)
                );

            List<TimeSeriesDTO> data;

            switch (groupBy)
            {
                case "day":
                    {
                        // ===== PHASE 1: EF (SQL) =====
                        var raw = baseQuery
                            .GroupBy(t => t.CreatedAt.Date)
                            .Select(g => new
                            {
                                Date = g.Key,
                                Total = g.Sum(x => x.Amount)
                            })
                            .OrderBy(x => x.Date)
                            .ToList();

                        // ===== PHASE 2: C# (FORMAT STRING) =====
                        data = raw.Select(x => new TimeSeriesDTO
                        {
                            Label = x.Date.ToString("dd/MM/yyyy"),
                            Value = x.Total
                        }).ToList();

                        break;
                    }

                case "month":
                    {
                        var raw = baseQuery
                            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
                            .Select(g => new
                            {
                                g.Key.Year,
                                g.Key.Month,
                                Total = g.Sum(x => x.Amount)
                            })
                            .OrderBy(x => x.Year)
                            .ThenBy(x => x.Month)
                            .ToList();

                        data = raw.Select(x => new TimeSeriesDTO
                        {
                            Label = $"{x.Month:D2}/{x.Year}",
                            Value = x.Total
                        }).ToList();

                        break;
                    }

                case "year":
                    {
                        var raw = baseQuery
                            .GroupBy(t => t.CreatedAt.Year)
                            .Select(g => new
                            {
                                Year = g.Key,
                                Total = g.Sum(x => x.Amount)
                            })
                            .OrderBy(x => x.Year)
                            .ToList();

                        data = raw.Select(x => new TimeSeriesDTO
                        {
                            Label = x.Year.ToString(),
                            Value = x.Total
                        }).ToList();

                        break;
                    }

                default:
                    data = new List<TimeSeriesDTO>();
                    break;
            }

            return new ResponseDTO("Revenue stats", 200, true, data);
        }

        public async Task<ResponseDTO> GetTripCreatedStats(DateTime from,DateTime to,string groupBy)
        {
            var trips = _unitOfWork.TripRepo.GetAll()
                .Where(x => x.CreateAt >= from && x.CreateAt <= to);

            var data = BuildTimeSeries(trips.Select(x => x.CreateAt), groupBy);

            return new ResponseDTO("Trip created over time", 200, true, data);
        }

        public async Task<ResponseDTO> GetTripStatsByStatus()
        {
            var trips = _unitOfWork.TripRepo.GetAll();

            var data = trips
                .GroupBy(x => x.Status)
                .Select(g => new StatusStatisticDTO
                {
                    Status = g.Key.ToString(),
                    Count = g.Count()
                })
                .ToList();

            return new ResponseDTO("Trip by status", 200, true, data);
        }

        public async Task<ResponseDTO> GetUserCountByRole()
        {
            var users = _unitOfWork.BaseUserRepo.GetAll();

            var data = users
                .GroupBy(x => x.Role.RoleName)
                .Select(g => new
                {
                    Role = g.Key,
                    Count = g.Count()
                })
                .ToList();

            return new ResponseDTO("User count by role", 200, true, data);
        }

        public async Task<ResponseDTO> GetUserRegistrationStats(
     DateTime from,
     DateTime to,
     string groupBy)
        {
            var users = _unitOfWork.BaseUserRepo.GetAll()
                .Where(x => x.CreatedAt >= from && x.CreatedAt <= to);

            var data = BuildTimeSeries(users.Select(x => x.CreatedAt), groupBy);

            return new ResponseDTO("User registration stats", 200, true, data);
        }
        private List<TimeSeriesDTO> BuildTimeSeries( IEnumerable<DateTime> dates,string groupBy)
        {
            return groupBy switch
            {
                "day" => dates
                    .GroupBy(d => d.Date)
                    .Select(g => new TimeSeriesDTO
                    {
                        Label = g.Key.ToString("dd/MM/yyyy"),
                        Value = g.Count()
                    })
                    .OrderBy(x => x.Label)
                    .ToList(),

                "month" => dates
                    .GroupBy(d => new { d.Year, d.Month })
                    .Select(g => new TimeSeriesDTO
                    {
                        Label = $"{g.Key.Month}/{g.Key.Year}",
                        Value = g.Count()
                    })
                    .OrderBy(x => x.Label)
                    .ToList(),

                "year" => dates
                    .GroupBy(d => d.Year)
                    .Select(g => new TimeSeriesDTO
                    {
                        Label = g.Key.ToString(),
                        Value = g.Count()
                    })
                    .OrderBy(x => x.Label)
                    .ToList(),

                _ => new List<TimeSeriesDTO>()
            };
        }
    }
}
