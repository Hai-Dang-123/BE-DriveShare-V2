﻿using Common.Enums.Status;
using Common.Enums.Type;
using System;
using System.Collections.Generic;

namespace Common.DTOs
{
    public class TripDetailFullDTO
    {
        public Guid TripId { get; set; }
        public string TripCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreateAt { get; set; }
        public DateTime UpdateAt { get; set; }

        // 🧩 Vehicle
        public VehicleSummaryDTO Vehicle { get; set; } = new();

        // 🧩 Owner
        public OwnerSummaryDTO Owner { get; set; } = new();

        // 🧩 Shipping Route
        public RouteDetailDTO ShippingRoute { get; set; } = new();

        // 🧩 Trip Route
        public TripRouteSummaryDTO TripRoute { get; set; } = new();

        // 🧩 Provider (nếu có PostTrip)
        public ProviderSummaryDTO? Provider { get; set; }

        // 🧩 Packages
        public List<string> PackageCodes { get; set; } = new();

        // 🧩 Drivers
        public List<TripDriverAssignmentDTO> Drivers { get; set; } = new();

        // 🧩 Contacts (Sender/Receiver)
        public List<TripContactDTO> Contacts { get; set; } = new();

        // 🧩 Contracts
        public List<ContractSummaryDTO> DriverContracts { get; set; } = new();
        public List<ContractSummaryDTO> ProviderContracts { get; set; } = new();

        // 🧩 Records, Compensation, Issues
        public List<TripDeliveryRecordDTO> DeliveryRecords { get; set; } = new();
        public List<TripCompensationDTO> Compensations { get; set; } = new();
        public List<TripDeliveryIssueDTO> Issues { get; set; } = new();
    }

    public class VehicleSummaryDTO
    {
        public Guid VehicleId { get; set; }
        public string PlateNumber { get; set; } = "";
        public string Model { get; set; } = "";
        public string VehicleTypeName { get; set; } = "";
    }

    public class OwnerSummaryDTO
    {
        public Guid OwnerId { get; set; }
        public string FullName { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
    }

    public class RouteDetailDTO
    {
        public string StartAddress { get; set; } = "";
        public string EndAddress { get; set; } = "";
        public decimal EstimatedDistanceKm { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
    }

    public class TripRouteSummaryDTO
    {
        public decimal DistanceKm { get; set; }
        public double DurationMinutes { get; set; }
    }

    public class ProviderSummaryDTO
    {
        public Guid ProviderId { get; set; }
        public string CompanyName { get; set; } = "";
        public string TaxCode { get; set; } = "";
        public decimal AverageRating { get; set; }
    }

    public class TripDriverAssignmentDTO
    {
        public Guid DriverId { get; set; }
        public string FullName { get; set; } = "";
        public string Type { get; set; } = "";
        public string AssignmentStatus { get; set; } = "";
        public string PaymentStatus { get; set; } = "";
    }

    public class TripContactDTO
    {
        public Guid TripContactId { get; set; }
        public string Type { get; set; } = "";
        public string FullName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string? Note { get; set; }
    }

    public class ContractSummaryDTO
    {
        public Guid ContractId { get; set; }
        public string ContractCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal ContractValue { get; set; }
        public string Currency { get; set; } = "VND";
        public DateTime? EffectiveDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? FileURL { get; set; }
    }


    public class TripDeliveryRecordDTO
    {
        public Guid TripDeliveryRecordId { get; set; }
        public string RecordType { get; set; } = "";
        public string? Note { get; set; }
        public DateTime CreateAt { get; set; }
    }

    public class TripCompensationDTO
    {
        public Guid TripCompensationId { get; set; }
        public string Reason { get; set; } = "";
        public decimal Amount { get; set; }
    }

    public class TripDeliveryIssueDTO
    {
        public Guid TripDeliveryIssueId { get; set; }
        public string IssueType { get; set; } = "";
        public string Description { get; set; } = "";
        public string Status { get; set; } = "";
    }
}
