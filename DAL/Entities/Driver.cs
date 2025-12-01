using Common.Enums.Status; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class Driver : BaseUser
    {
        public string LicenseNumber { get; set; } = null!;
        public string LicenseClass { get; set; } = null!; 
        public DateTime? LicenseExpiryDate { get; set; }         
        public bool IsLicenseVerified { get; set; } = false; // Đã xác thực bằng lái?
        //public bool IsInTrip { get; set; } = false;
        public DriverStatus DriverStatus { get; set; } 

        //

        public virtual ICollection<DriverWorkSession> DriverWorkSessions { get; set; } = new List<DriverWorkSession>();
        public virtual ICollection<OwnerDriverLink> OwnerDriverLinks { get; set; } = new List<OwnerDriverLink>();
        public virtual ICollection<TripDriverAssignment> TripDriverAssignments { get; set; } = new List<TripDriverAssignment>();
        public virtual ICollection<DriverActivityLog> ActivityLogs { get; set; } = new List<DriverActivityLog>();
        public virtual ICollection<TripDeliveryRecord> TripDeliveryRecords { get; set; } = new List<TripDeliveryRecord>();
        public virtual ICollection<TripDriverContract> TripDriverContracts { get; set;} = new List<TripDriverContract>();
        public virtual ICollection<TripVehicleHandoverRecord> TripVehicleHandoverRecords { get; set; } = new List<TripVehicleHandoverRecord>();
    }
}