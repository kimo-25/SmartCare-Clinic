using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SCMS.BL.BLInterfaces;
using SCMS.Models;

namespace SCMS.BL.BLClasses
{
    public class ChatService : IChatService
    {
        private readonly AppDbContext _context;

        public ChatService(AppDbContext context)
        {
            _context = context;
        }

        // ✅ أهم تعديل:
        // لو receptionistId == null (المريض بيفتح الشات من زرار Start)
        // هنجيب أي Thread "Open" للمريض سواء اتاخد من ريسيبشن أو لسه unassigned
        public ChatThread GetOrCreateThread(int patientId, int? receptionistId = null)
        {
            ChatThread? thread;

            if (receptionistId == null)
            {
                thread = _context.ChatThreads
                    .FirstOrDefault(t => t.PatientId == patientId && t.Status == "Open");
            }
            else
            {
                thread = _context.ChatThreads
                    .FirstOrDefault(t => t.PatientId == patientId &&
                                         t.ReceptionistId == receptionistId &&
                                         t.Status == "Open");
            }

            if (thread != null) return thread;

            thread = new ChatThread
            {
                PatientId = patientId,
                ReceptionistId = receptionistId, // غالبًا null
                Status = "Open",
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatThreads.Add(thread);
            _context.SaveChanges();
            return thread;
        }

        public ChatThread? GetThreadById(int threadId)
        {
            return _context.ChatThreads
                .Include(t => t.Patient)
                .Include(t => t.Receptionist)
                .FirstOrDefault(t => t.ThreadId == threadId);
        }

        public List<ChatThread> GetThreadsForPatient(int patientId)
        {
            return _context.ChatThreads
                .Where(t => t.PatientId == patientId)
                .OrderByDescending(t => t.LastMessageAt ?? t.CreatedAt)
                .Include(t => t.Receptionist)
                .ToList();
        }

        public List<ChatThread> GetThreadsForReceptionist(int receptionistId)
        {
            return _context.ChatThreads
                .Where(t => t.ReceptionistId == receptionistId)
                .OrderByDescending(t => t.LastMessageAt ?? t.CreatedAt)
                .Include(t => t.Patient)
                .ToList();
        }

        // ✅ Unassigned (Receptionist Inbox)
        public List<ChatThread> GetUnassignedThreads()
        {
            return _context.ChatThreads
                .Where(t => t.ReceptionistId == null && t.Status == "Open")
                .OrderByDescending(t => t.LastMessageAt ?? t.CreatedAt)
                .Include(t => t.Patient)
                .ToList();
        }

        // ✅ Take / Assign
        public bool AssignThreadToReceptionist(int threadId, int receptionistId)
        {
            var thread = _context.ChatThreads.FirstOrDefault(t => t.ThreadId == threadId);
            if (thread == null) return false;

            if (thread.Status == "Closed") return false;
            if (thread.ReceptionistId.HasValue) return false; // already taken

            thread.ReceptionistId = receptionistId;
            _context.SaveChanges();
            return true;
        }

        public List<ChatMessage> GetMessages(int threadId, int take = 100, int skip = 0)
        {
            if (take <= 0) take = 100;
            if (take > 500) take = 500;
            if (skip < 0) skip = 0;

            return _context.ChatMessages
                .Where(m => m.ThreadId == threadId)
                .OrderByDescending(m => m.SentAt)
                .Skip(skip)
                .Take(take)
                .Include(m => m.SenderUser)
                .Include(m => m.ReceiverUser)
                .AsNoTracking()
                .OrderBy(m => m.SentAt)
                .ToList();
        }

        private static UserType GetUserType(User u)
        {
            if (u is Patient) return UserType.Patient;
            if (u is Doctor) return UserType.Doctor;
            if (u is Radiologist) return UserType.Radiologist;
            if (u is Receptionist) return UserType.Receptionist;
            if (u is Admin) return UserType.Admin;
            if (u is Staff) return UserType.Staff;
            return UserType.User;
        }

        public ChatMessage? SendMessage(int threadId, int senderUserId, string content, int? receiverUserId = null)
        {
            var thread = _context.ChatThreads.FirstOrDefault(t => t.ThreadId == threadId);
            if (thread == null) return null;
            if (thread.Status == "Closed") return null;

            if (string.IsNullOrWhiteSpace(content)) return null;

            var sender = _context.Users.FirstOrDefault(u => u.UserId == senderUserId);
            if (sender == null) return null;

            if (receiverUserId.HasValue && !_context.Users.Any(u => u.UserId == receiverUserId.Value))
                return null;

            var message = new ChatMessage
            {
                ThreadId = threadId,
                SenderUserId = senderUserId,
                ReceiverUserId = receiverUserId,
                SenderType = GetUserType(sender),
                Content = content.Trim(),
                IsRead = false,
                SentAt = DateTime.UtcNow
            };

            _context.ChatMessages.Add(message);
            thread.LastMessageAt = message.SentAt;
            _context.SaveChanges();

            return message;
        }

        public int GetUnreadCountForUser(int userId)
        {
            return _context.ChatMessages.Count(m => m.ReceiverUserId == userId && !m.IsRead);
        }

        public int GetUnreadCountForThread(int threadId, int receiverUserId)
        {
            return _context.ChatMessages.Count(m =>
                m.ThreadId == threadId &&
                m.ReceiverUserId == receiverUserId &&
                !m.IsRead
            );
        }

        public bool MarkThreadAsRead(int threadId, int userId)
        {
            var msgs = _context.ChatMessages
                .Where(m => m.ThreadId == threadId && m.ReceiverUserId == userId && !m.IsRead)
                .ToList();

            foreach (var m in msgs)
                m.IsRead = true;

            _context.SaveChanges();
            return true;
        }

        public bool CloseThread(int threadId)
        {
            var thread = _context.ChatThreads.FirstOrDefault(t => t.ThreadId == threadId);
            if (thread == null) return false;

            thread.Status = "Closed";
            _context.SaveChanges();
            return true;
        }

        public bool ReopenThread(int threadId)
        {
            var thread = _context.ChatThreads.FirstOrDefault(t => t.ThreadId == threadId);
            if (thread == null) return false;

            thread.Status = "Open";
            _context.SaveChanges();
            return true;
        }
    }
}
