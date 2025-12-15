using Microsoft.EntityFrameworkCore;
using SCMS.BL;
using SCMS.BL.BLClasses;
using SCMS.BL.BLInterfaces;
using SCMS.Models;

namespace SCMS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("Default"))
            );

            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(6);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IStaffService, StaffService>();
            builder.Services.AddScoped<IDoctorService, DoctorService>();
            builder.Services.AddScoped<IReceptionService, ReceptionService>();
            builder.Services.AddScoped<IPatientService, PatientService>();
            builder.Services.AddScoped<IPatientProfileService, PatientProfileService>();
            builder.Services.AddScoped<IDoctorProfileService, DoctorProfileService>();
            builder.Services.AddScoped<IAppointmentService, AppointmentService>();
            builder.Services.AddScoped<IAppointmentBookingService, AppointmentBookingService>();
            builder.Services.AddScoped<IInvoiceService, InvoiceService>();
            builder.Services.AddScoped<IFeedbackService, FeedbackService>();
            builder.Services.AddScoped<IPrescriptionService, PrescriptionService>();
            builder.Services.AddScoped<IMedicalRecordService, MedicalRecordService>();
            builder.Services.AddScoped<IRadiologyService, RadiologyService>();
            builder.Services.AddScoped<IAdminService, AdminService>();
            builder.Services.AddScoped<IChatService, ChatService>();

            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseSession();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Account}/{action=Login}/{id?}"
            );

            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

              
                var adminExists = context.Users.OfType<Admin>()
                    .Any(a => a.Username == "admin" || a.Email == "admin@scms.com");

                if (!adminExists)
                {
                    var admin = new Admin
                    {
                        FullName = "System Admin",
                        Email = "admin@scms.com",
                        Username = "admin",
                        Phone = "01000000000", 
                        PasswordHash = authService.HashPassword("Admin@123"),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        AccessLevel = "Full"
                    };

                    context.Users.Add(admin);
                    context.SaveChanges();
                }
            }

            app.Run();
        }
    }
}
