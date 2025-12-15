using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SCMS.BL.BLInterfaces;
using SCMS.Models;

namespace SCMS.BL.BLClasses
{
    public class MedicalRecordService : IMedicalRecordService
    {
        private readonly AppDbContext _context;

        public MedicalRecordService(AppDbContext context)
        {
            _context = context;
        }

        public MedicalRecord CreateManualRecord(int patientId, string? description, int? prescriptionId = null, int? radiologyResultId = null)
        {
            var record = new MedicalRecord
            {
                PatientId = patientId,
                Description = description,
                PrescriptionId = prescriptionId,
                RadiologyResultId = radiologyResultId,
                RecordDate = DateTime.UtcNow
            };

            _context.MedicalRecords.Add(record);
            _context.SaveChanges();
            return record;
        }

        public MedicalRecord? AddRecordFromPrescription(int prescriptionId, string? description = null)
        {
            var prescription = _context.Prescriptions.FirstOrDefault(p => p.PrescriptionId == prescriptionId);
            if (prescription == null) return null;

            var record = new MedicalRecord
            {
                PatientId = prescription.PatientId,
                PrescriptionId = prescription.PrescriptionId,
                Description = description ?? "Prescription created",
                RecordDate = DateTime.UtcNow
            };

            _context.MedicalRecords.Add(record);
            _context.SaveChanges();
            return record;
        }

        public MedicalRecord? AddRecordFromRadiologyResult(int radiologyResultId, string? description = null)
        {
            var result = _context.RadiologyResults
                .Include(r => r.Request)
                .FirstOrDefault(r => r.ResultId == radiologyResultId);

            if (result?.Request == null) return null;

            var record = new MedicalRecord
            {
                PatientId = result.Request.PatientId,
                RadiologyResultId = result.ResultId,
                Description = description ?? "Radiology result added",
                RecordDate = DateTime.UtcNow
            };

            _context.MedicalRecords.Add(record);
            _context.SaveChanges();
            return record;
        }

        public MedicalRecord? GetById(int recordId)
        {
            return _context.MedicalRecords
                .Include(r => r.RelatedPrescription)
                .Include(r => r.RadiologyResult!)
                    .ThenInclude(rr => rr.Request!)

                .FirstOrDefault(r => r.RecordId == recordId);
        }

        public List<MedicalRecord> GetByPatient(int patientId)
        {
            return _context.MedicalRecords
                .Where(r => r.PatientId == patientId)
                .Include(r => r.RelatedPrescription)
                .Include(r => r.RadiologyResult!)
                    .ThenInclude(rr => rr.Request!)

                .OrderByDescending(r => r.RecordDate)
                .ToList();
        }

    }
}
