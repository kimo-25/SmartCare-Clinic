using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace SCMS.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Patient> Patients { get; set; } = null!;
        public DbSet<Staff> Staff { get; set; } = null!;
        public DbSet<Doctor> Doctors { get; set; } = null!;
        public DbSet<Radiologist> Radiologists { get; set; } = null!;
        public DbSet<Receptionist> Receptionists { get; set; } = null!;
        public DbSet<Admin> Admins { get; set; } = null!;

        public DbSet<Appointment> Appointments { get; set; } = null!;
        public DbSet<AppointmentBooking> AppointmentBookings { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<Prescription> Prescriptions { get; set; } = null!;
        public DbSet<MedicalRecord> MedicalRecords { get; set; } = null!;
        public DbSet<Feedback> Feedbacks { get; set; } = null!;
        public DbSet<RadiologyRequest> RadiologyRequests { get; set; } = null!;
        public DbSet<RadiologyResult> RadiologyResults { get; set; } = null!;
        public DbSet<ChatThread> ChatThreads { get; set; } = null!;
        public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
        public DbSet<ChatAttachment> ChatAttachments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== TPH Discriminator على User =====
            modelBuilder.Entity<User>()
                .HasDiscriminator<string>("Discriminator")
                .HasValue<User>("User")
                .HasValue<Patient>("Patient")
                .HasValue<Staff>("Staff")
                .HasValue<Doctor>("Doctor")
                .HasValue<Radiologist>("Radiologist")
                .HasValue<Receptionist>("Receptionist")
                .HasValue<Admin>("Admin");

            // ===== Relations =====
            modelBuilder.Entity<AppointmentBooking>()
                .HasOne(b => b.Appointment)
                .WithMany(a => a.Bookings)
                .HasForeignKey(b => b.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AppointmentBooking>()
                .HasOne(b => b.Patient)
                .WithMany(p => p.AppointmentBookings)
                .HasForeignKey(b => b.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.BookingId)
                .IsUnique();

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.AppointmentBooking)
                .WithOne(b => b.Invoice)
                .HasForeignKey<Invoice>(i => i.BookingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.Patient)
                .WithMany(p => p.Feedbacks)
                .HasForeignKey(f => f.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.Doctor)
                .WithMany(d => d.Feedbacks)
                .HasForeignKey(f => f.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Prescription>()
                .HasOne(p => p.Patient)
                .WithMany(pt => pt.Prescriptions)
                .HasForeignKey(p => p.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RadiologyRequest>()
                .HasOne(r => r.Patient)
                .WithMany(p => p.RadiologyRequests)
                .HasForeignKey(r => r.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RadiologyRequest>()
                .HasOne(r => r.Result)
                .WithOne(res => res.Request)
                .HasForeignKey<RadiologyResult>(res => res.RequestId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MedicalRecord>()
                .HasOne(m => m.Patient)
                .WithMany(p => p.MedicalRecords)
                .HasForeignKey(m => m.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MedicalRecord>()
                .HasOne(m => m.RadiologyResult)
                .WithMany(r => r.MedicalRecords)
                .HasForeignKey(m => m.RadiologyResultId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatThread>()
                .HasOne(t => t.Patient)
                .WithMany(p => p.ChatThreads)
                .HasForeignKey(t => t.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatThread>()
                .HasOne(t => t.Receptionist)
                .WithMany(r => r.ChatThreads)
                .HasForeignKey(t => t.ReceptionistId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Thread)
                .WithMany(t => t.Messages)
                .HasForeignKey(m => m.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.SenderUser)
                .WithMany()
                .HasForeignKey(m => m.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.ReceiverUser)
                .WithMany()
                .HasForeignKey(m => m.ReceiverUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatAttachment>()
                .HasOne(a => a.Message)
                .WithMany()
                .HasForeignKey(a => a.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== Prevent multiple cascade paths =====
            var keepCascade = new HashSet<string>
            {
                "FK_ChatMessages_ChatThreads_ThreadId",
                "FK_ChatAttachments_ChatMessages_MessageId"
            };

            foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                if (fk.IsOwnership) continue;
                if (keepCascade.Contains(fk.GetConstraintName() ?? "")) continue;

                if (fk.DeleteBehavior == DeleteBehavior.Cascade)
                    fk.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }
}
