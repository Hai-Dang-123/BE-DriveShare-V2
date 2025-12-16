using BLL.Services.Interface;
using Common.DTOs;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            var users =  _unitOfWork.BaseUserRepo.GetAll();
            var trips =  _unitOfWork.TripRepo.GetAll();
            var packages =  _unitOfWork.PackageRepo.GetAll();

            var data = new DashboardOverviewDTO
            {
                TotalUsers = users.Count(),
                TotalDrivers = users.Count(x => x.Role.RoleName == "DRIVER"),
                TotalOwners = users.Count(x => x.Role.RoleName == "OWNER"),
                TotalProviders = users.Count(x => x.Role.RoleName == "PROVIDER"),
                TotalTrips = trips.Count(),
                TotalPackages = packages.Count(),
                TotalRevenue = 0
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
        public async Task<ResponseDTO> GetRevenueStats(DateTime from,DateTime to,string groupBy)
        {
            var data = new List<TimeSeriesDTO>();

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
