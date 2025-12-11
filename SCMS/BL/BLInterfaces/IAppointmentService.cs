using SCMS.Models;
using System.Collections.Generic;

namespace SCMS.BL.BLInterfaces
{
    public interface IAppointmentService
    {
        Appointment? Create(Appointment slot);
        bool Update(Appointment slot);
        bool Delete(int appointmentId);

        Appointment? GetById(int appointmentId);
        List<Appointment> GetAll();
        List<Appointment> GetAvailable();
        List<Appointment> GetByDoctor(int doctorId);
    }
}
