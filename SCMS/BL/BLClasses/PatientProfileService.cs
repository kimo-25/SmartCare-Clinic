using System.Linq;
using Microsoft.EntityFrameworkCore;
using SCMS.BL.BLInterfaces;
using SCMS.Models;
using SCMS.ViewModels;

namespace SCMS.BL.BLClasses
{
    public class PatientProfileService : IPatientProfileService
    {
        private readonly AppDbContext _context;

        public PatientProfileService(AppDbContext context)
        {
            _context = context;
        }

        public PatientProfileVm? GetPatientProfileForDoctor(int patientId, int doctorId)
        {
            bool hasRelation = _context.AppointmentBookings
                .Include(b => b.Appointment)
                .Any(b => b.PatientId == patientId && b.Appointment.DoctorId == doctorId);

            if (!hasRelation)
                return null;

            var patient = _context.Patients
                .Include(p => p.MedicalRecords)
                    .ThenInclude(r => r.RelatedPrescription)
                .Include(p => p.MedicalRecords)
                    .ThenInclude(r => r.RadiologyResult!)
                        .ThenInclude(rr => rr.Request!)
                .FirstOrDefault(p => p.UserId == patientId);

            if (patient == null)
                return null;

            return new PatientProfileVm
            {
                PatientId = patient.UserId,
                FullName = patient.FullName,
                Age = patient.Age,
                Gender = patient.Gender,
                Address = patient.Address,
                MedicalHistorySummary = patient.MedicalHistorySummary,
                Records = patient.MedicalRecords
                    .OrderByDescending(r => r.RecordDate)
                    .Select(r => new PatientProfileRecordVm
                    {
                        RecordId = r.RecordId,
                        RecordDate = r.RecordDate,
                        Description = r.Description,
                        Diagnosis = r.RelatedPrescription?.Diagnosis,
                        Treatment = r.RelatedPrescription?.Treatment,
                        RadiologyTestName = r.RadiologyResult?.Request?.TestName,
                        RadiologyStatus = r.RadiologyResult?.Status
                    })
                    .ToList()
            };
        }
    }
}
