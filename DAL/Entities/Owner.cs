using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class Owner : BaseUser
    {
        public string? CompanyName { get; set; } = null!;
        public string? TaxCode { get; set; } = null!;
        public Location? BusinessAddress { get; set; } = null!;
        public decimal? AverageRating { get; set; } = null!;

        //
        public virtual ICollection<OwnerDriverLink> OwnerDriverLinks { get; set; } = new List<OwnerDriverLink>();
        public virtual ICollection<PostTrip> PostTrips { get; set; } = new List<PostTrip>();
        public virtual ICollection<Package> Packages { get; set; } = new List<Package>();
        public virtual ICollection<Item> Items { get; set; } = new List<Item>();
        public virtual ICollection<Trip> Trips { get; set; } = new List<Trip>();
        public virtual ICollection<TripProviderContract> TripProviderContracts { get; set; } = new List<TripProviderContract>();
        public virtual ICollection<BaseContract> Contracts { get; set; } = new List<BaseContract>();
        public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    }
}
