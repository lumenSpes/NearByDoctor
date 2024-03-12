using ASM.EF;
using ASM.Models;
using ASM.Models.DTOs;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using MimeKit.Text;
using Newtonsoft.Json;
using Rotativa.AspNetCore;
using System.Configuration;

namespace ASM.Controllers
{
    public class AppointmentsController : Controller
    {
        private readonly DataContext _context;

        public AppointmentsController(DataContext context)
        {
            _context = context;
        }
        [HttpGet]
        [Route("appointment/index")]
        public IActionResult Index()
        {
            var email = HttpContext.Session.GetString("email");
            var doctor = _context.Doctors.Where(x => x.Email == email).FirstOrDefault();

            var appointments = _context.Appointments
                .Include(a => a.Doctor)
                .ThenInclude(d => d.Hospital)
                .ThenInclude(h => h.Location)
                .Include(a => a.Patient)  // Include the Patient navigation property
                .Where(x => x.DoctorId == doctor.Id)
                .Select(a => new AppointmentDTO
                {
                    Id = a.Id,
                    Date = a.Date,
                    LocationName = a.Doctor.Hospital.Location.Name,
                    HospitalName = a.Doctor.Hospital.Name,
                    CategoryName = a.Doctor.Category.Name,
                    DoctorName = a.Doctor.Name,
                    PatientName = a.Patient.Name, // Include the patient name in the DTO
                    PatientId = a.PatientId,
                    Note = a.Note
                })
                .ToList();

            return View(appointments);
        }


        [HttpGet]
        [Route("patient/appointment")]
        public IActionResult PatientAppointment()
        {
            var email = HttpContext.Session.GetString("email");
            var patient = _context.Patients.Where(x => x.Email == email).FirstOrDefault();
            var appointments = _context.Appointments
                .Include(a => a.Doctor)
                .ThenInclude(d => d.Hospital)
                .ThenInclude(h => h.Location)
                .Where(x => x.PatientId == patient.Id)
                .Select(a => new AppointmentDTO
                {
                    Id = a.Id,
                    Date = a.Date,
                    LocationName = a.Doctor.Hospital.Location.Name,
                    HospitalName = a.Doctor.Hospital.Name,
                    CategoryName = a.Doctor.Category.Name,
                    DoctorName = a.Doctor.Name,
                    DoctorId = a.Doctor.Id,
                    Note = a.Note
                })
                .ToList();

            return View(appointments);
        }

        [HttpGet]
        [Route("patient/delete-appointment/{id}")]
        public IActionResult DeletePatientAppointment(int id)
        {
            var email = HttpContext.Session.GetString("email");
            var user = _context.Patients.FirstOrDefault(x => x.Email == email);
            var exObj = _context.Appointments.Find(id);
            if(exObj != null)
            {
                _context.Appointments.Remove(exObj);
                _context.SaveChanges();

                string from = "arkosams938@gmail.com";
                string to = user.Email;
                string sub = "Appointment cancelation summary!";
                string body = "Your appointment has been canceled!";

                SendEmail(from, to, sub, body);
            }
            return RedirectToAction("PatientAppointment");
        }

        [HttpGet]
        [Route("doctor/delete-appointment/{id}")]
        public IActionResult DeleteDoctorAppointment(int id)
        {
            var exObj = _context.Appointments.Find(id);
            if (exObj != null)
            {
                try
                {
                    var user = _context.Patients.FirstOrDefault(x => x.Id == exObj.PatientId);

                    if (user != null)
                    {
                        _context.Appointments.Remove(exObj);
                        _context.SaveChanges();

                        string from = "arkosams938@gmail.com";
                        string to = user.Email;
                        string sub = "Appointment cancellation summary!";
                        string body = "Your appointment has been canceled!";

                        SendEmail(from, to, sub, body);
                    }
                }
                catch (Exception ex)
                {
                    // Handle or log the exception as needed
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }

            return RedirectToAction("Index");
        }

        private void SendEmail(string from, string to, string body, string sub)
        {
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(from));
            email.To.Add(MailboxAddress.Parse(to));
            email.Subject = sub;
            email.Body = new TextPart(TextFormat.Html)
            {
                Text = body
            };

            using var smtp = new SmtpClient();
            smtp.Connect("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            smtp.Authenticate("arkosams938@gmail.com", "dgbuyzhjriiliglo");
            smtp.Send(email);
            smtp.Disconnect(true);
        }

        [HttpGet]
        [Route("pay/{id}")]
        public IActionResult Pay(int id)
        {
            var doctor = _context.Doctors.Where(x => x.Id == id).FirstOrDefault();
            var email = HttpContext.Session.GetString("email");
            var patient = _context.Patients.Where(x => x.Email == email).FirstOrDefault();
            TempData["doctor"] = JsonConvert.SerializeObject(doctor);
            TempData["patient"] = JsonConvert.SerializeObject(patient);
            return View();
        }


        [HttpPost]
        [Route("pay/{id}")]
        public IActionResult Pay(int id, Payment payment)
        {
            var doctorJson = TempData["doctor"] as string;
            var patientJson = TempData["patient"] as string;
            var doctor = JsonConvert.DeserializeObject<Doctor>(doctorJson);
            var patient = JsonConvert.DeserializeObject<Patient>(patientJson);

            var pay = new Payment
            {
                TrxId = payment.TrxId,
                PatientId = patient.Id,
                DoctorId = doctor.Id
            };

            _context.Payments.Add(pay);
            _context.SaveChanges();

            return RedirectToAction("Thankyou");

        }
        [HttpGet]
        public IActionResult Thankyou()
        {
            return View();
        }

        [HttpGet]
        [Route("appointment/create")]
        public IActionResult Create()
        {
            var email = HttpContext.Session.GetString("email");
            var patient = _context.Patients.Where(x => x.Email == email).FirstOrDefault();


            // Create an instance of the Appointment model
            var appointment = new Appointment
            {
                // Assign patient-related properties as needed
                PatientId = patient.Id,
                // Other properties...
            };

            // Populate location and category dropdowns
            ViewBag.Locations = GetLocations();
            ViewBag.Catagories = GetCatagories();

            return View(appointment);
        }

        [HttpPost]
        [Route("appointment/create")]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Appointment Appointment)
        {
            _context.Add(Appointment);
            _context.SaveChanges();
            return RedirectToAction("Patient", "Dashboard");
        }

        [Route("appointment/book/{id}")]
        public IActionResult Book(int id)
        {
            var doctor = _context.Doctors.Where(x => x.Id == id).FirstOrDefault();
            var email = HttpContext.Session.GetString("email");
            var patient = _context.Patients.Where(x => x.Email == email).FirstOrDefault();
            var appintment = new Appointment
            {
                Date = DateTime.Now.Date.AddDays(1),
                LocationId = doctor.LoacationId,
                HospitalId = doctor.HospitalId,
                CatagoryId = doctor.CategoryId,
                DoctorId = doctor.Id,
                Note = "I AM SO SICK",
                PatientId = patient.Id
            };

            _context.Appointments.Add(appintment);
            _context.SaveChanges();

            return RedirectToAction("PatientAppointment");
        }

        private List<SelectListItem> GetLocations()
        {
            var lstLocations = new List<SelectListItem>();

            List<Location> Locations = _context.Locations.ToList();

            lstLocations = Locations.Select(ct => new SelectListItem()
            {
                Value = ct.Id.ToString(),
                Text = ct.Name
            }).ToList();

            var defItem = new SelectListItem()
            {
                Value = "",
                Text = "----Select Location----"
            };

            lstLocations.Insert(0, defItem);

            return lstLocations;
        }

        [HttpGet]
        public JsonResult GetHospitalsByLocation(int locationId)
        {
            List<SelectListItem> hospitals = _context.Hospitals
                .Where(c => c.LocationId == locationId)
                .OrderBy(n => n.Name)
                .Select(n => new SelectListItem
                {
                    Value = n.Id.ToString(),
                    Text = n.Name
                }).ToList();

            return Json(hospitals);
        }

        private List<SelectListItem> GetCatagories()
        {
            var lstCatagories = new List<SelectListItem>();

            List<Category> Catagories = _context.Categories.ToList();

            lstCatagories = Catagories.Select(ct => new SelectListItem()
            {
                Value = ct.Id.ToString(),
                Text = ct.Name
            }).ToList();

            var defItem = new SelectListItem()
            {
                Value = "",
                Text = "----Select Category----"
            };

            lstCatagories.Insert(0, defItem);

            return lstCatagories;
        }

        [HttpGet]
        public JsonResult GetDoctorsByCategoryAndHospital(int categoryId, int hospitalId)
        {
            List<SelectListItem> doctors = _context.Doctors
                .Where(d => d.CategoryId == categoryId && d.HospitalId == hospitalId)
                .OrderBy(d => d.Name)
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = d.Name
                }).ToList();

            return Json(doctors);
        }



        [HttpGet]
        [Route("appointment/details/{id}")]
        public IActionResult Details(int id)
        {
            var appointment = _context.Appointments
                .Include(a => a.Doctor)
                .ThenInclude(d => d.Hospital)
                .ThenInclude(h => h.Location)
                .FirstOrDefault(a => a.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            var appointmentDTO = new AppointmentDTO
            {
                Id = appointment.Id,
                Date = appointment.Date,
                LocationName = appointment.Doctor.Hospital.Location.Name,
                HospitalName = appointment.Doctor.Hospital.Name,
                CategoryName = appointment.Doctor.Category.Name,
                DoctorName = appointment.Doctor.Name,
                Note = appointment.Note
            };

            return View(appointmentDTO);
        }



        [HttpGet]
        [Route("appointment/delete/{id}")]
        public IActionResult Delete(int id)
        {
            var appointment = _context.Appointments.Find(id);

            if (appointment == null)
            {
                return NotFound();
            }

            return View(appointment);
        }

        [HttpPost, ActionName("Delete")]
        [Route("appointment/delete/{id}")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var appointment = _context.Appointments.Find(id);

            if (appointment == null)
            {
                return NotFound();
            }

            _context.Appointments.Remove(appointment);
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }



        private bool AppointmentExists(int id)
        {
            return _context.Appointments.Any(a => a.Id == id);
        }

        [HttpGet]
        [Route("appointments/print")]
        public IActionResult PrintAppointments()
        {
            var appointments = _context.Appointments
                .Include(a => a.Doctor)
                .ThenInclude(d => d.Hospital)
                .ThenInclude(h => h.Location)
                .Include(a => a.Patient)
                .Select(a => new AppointmentDTO
                {
                    Date = a.Date,
                    LocationName = a.Doctor.Hospital.Location.Name,
                    HospitalName = a.Doctor.Hospital.Name,
                    CategoryName = a.Doctor.Category.Name,
                    DoctorName = a.Doctor.Name,
                    PatientName = a.Patient.Name,
                    PatientId = a.PatientId,
                    Note = a.Note
                })
                .ToList();

            // Specify the view name and model
            var pdf = new ViewAsPdf("PrintAppointments", appointments)
            {
                CustomSwitches = "--no-images"
            };

            // Generate a unique file name based on today's date and "appointment"
            var fileName = $"{DateTime.Now:yyyyMMdd}_appointment.pdf";

            // Generate the PDF byte array
            var pdfBytes = pdf.BuildFile(ControllerContext).Result; // Await the result

            // Save the PDF to a file with the unique filename
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Rotativa", fileName);
            System.IO.File.WriteAllBytes(filePath, pdfBytes);

            // Return the file path for download
            return File(pdfBytes, "application/pdf", fileName);
        }

    }
}
