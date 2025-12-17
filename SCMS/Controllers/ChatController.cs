using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCMS.BL.BLInterfaces;
using SCMS.Models;
using SCMS.ViewModels;
using System.Linq;

namespace SCMS.Controllers
{
    public class ChatController : Controller
    {
        private readonly IChatService _chatService;
        private readonly AppDbContext _context;

        public ChatController(IChatService chatService, AppDbContext context)
        {
            _chatService = chatService;
            _context = context;
        }

        private int CurrentUserId()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            return int.TryParse(userIdStr, out var id) ? id : 0;
        }

        private UserType CurrentUserType()
        {
            var t = HttpContext.Session.GetInt32("UserType");
            return t.HasValue ? (UserType)t.Value : UserType.User;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var userId = CurrentUserId();
            var type = CurrentUserType();
            if (userId == 0) return RedirectToAction("Login", "Account");

            // Patient => يبدأ شات مباشرة
            if (type == UserType.Patient)
                return RedirectToAction(nameof(Start));

            // Receptionist => Inbox
            if (type == UserType.Receptionist)
            {
                var receptionist = _context.Set<Receptionist>()
                    .AsNoTracking()
                    .FirstOrDefault(r => r.UserId == userId);

                if (receptionist == null) return NotFound();

                var receptionistId = receptionist.UserId;

                var unassignedThreads = _chatService.GetUnassignedThreads();
                var myThreads = _chatService.GetThreadsForReceptionist(receptionistId);

                var vm = new ChatInboxVm
                {
                    Unassigned = unassignedThreads.Select(t => new ChatThreadRowVm
                    {
                        ThreadId = t.ThreadId,
                        PatientName = t.Patient?.FullName ?? "Unknown",
                        CreatedAt = t.CreatedAt,
                        LastMessageAt = t.LastMessageAt,
                        // لسه unassigned => اعتبر أي messages غير مقروءة كـ unread
                        UnreadCount = _context.ChatMessages.Count(m => m.ThreadId == t.ThreadId && !m.IsRead),
                        IsAssigned = false
                    }).ToList(),

                    Mine = myThreads.Select(t => new ChatThreadRowVm
                    {
                        ThreadId = t.ThreadId,
                        PatientName = t.Patient?.FullName ?? "Unknown",
                        CreatedAt = t.CreatedAt,
                        LastMessageAt = t.LastMessageAt,
                        // هنا receiver = receptionistId
                        UnreadCount = _chatService.GetUnreadCountForThread(t.ThreadId, receptionistId),
                        IsAssigned = true
                    }).ToList()
                };

                return View(vm);
            }

            return Forbid();
        }

        [HttpGet]
        public IActionResult Start()
        {
            var userId = CurrentUserId();
            var type = CurrentUserType();
            if (userId == 0) return RedirectToAction("Login", "Account");
            if (type != UserType.Patient) return Forbid();

            var thread = _chatService.GetOrCreateThread(patientId: userId, receptionistId: null);
            return RedirectToAction(nameof(Conversation), new { id = thread.ThreadId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Take(int id)
        {
            var userId = CurrentUserId();
            var type = CurrentUserType();
            if (userId == 0) return RedirectToAction("Login", "Account");
            if (type != UserType.Receptionist) return Forbid();

            var receptionist = _context.Set<Receptionist>()
                .AsNoTracking()
                .FirstOrDefault(r => r.UserId == userId);

            if (receptionist == null) return NotFound();

            var receptionistId = receptionist.UserId;

            var ok = _chatService.AssignThreadToReceptionist(id, receptionistId);
            if (!ok) return BadRequest("Cannot take this chat (already assigned/closed/not found)");

            return RedirectToAction(nameof(Conversation), new { id });
        }

        [HttpGet]
        public IActionResult Conversation(int id)
        {
            var userId = CurrentUserId();
            var type = CurrentUserType();
            if (userId == 0) return RedirectToAction("Login", "Account");

            var thread = _chatService.GetThreadById(id);
            if (thread == null) return NotFound();

            if (!CanAccessThread(type, userId, thread))
                return Forbid();

            var messages = _chatService.GetMessages(id);

            var receptionistName = thread.Receptionist != null
                ? thread.Receptionist.FullName
                : "Customer Care Supervisor";

            var vm = new ChatConversationVm
            {
                ConversationId = thread.ThreadId,
                AgentName = receptionistName,
                Messages = messages.Select(m => new ChatMessageVm
                {
                    MessageId = m.MessageId,
                    SenderName = m.SenderUser?.FullName ?? "Unknown",
                    IsFromCurrentUser = m.SenderUserId == userId,
                    Text = m.Content,
                    SentAt = m.SentAt
                }).ToList()
            };

            _chatService.MarkThreadAsRead(id, userId);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Send(ChatConversationVm vm)
        {
            var userId = CurrentUserId();
            var type = CurrentUserType();
            if (userId == 0) return RedirectToAction("Login", "Account");

            if (vm.ConversationId <= 0) return BadRequest();

            if (string.IsNullOrWhiteSpace(vm.NewMessageText))
                return RedirectToAction(nameof(Conversation), new { id = vm.ConversationId });

            var thread = _chatService.GetThreadById(vm.ConversationId);
            if (thread == null) return NotFound();

            if (!CanAccessThread(type, userId, thread))
                return Forbid();

            var receiverUserId = ResolveReceiverUserId(type, thread);

            _chatService.SendMessage(
                threadId: vm.ConversationId,
                senderUserId: userId,
                content: vm.NewMessageText,
                receiverUserId: receiverUserId
            );

            return RedirectToAction(nameof(Conversation), new { id = vm.ConversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult End(int id)
        {
            var userId = CurrentUserId();
            var type = CurrentUserType();
            if (userId == 0) return RedirectToAction("Login", "Account");

            var thread = _chatService.GetThreadById(id);
            if (thread == null) return NotFound();

            if (!CanAccessThread(type, userId, thread))
                return Forbid();

            _chatService.CloseThread(id);

            if (type == UserType.Patient)
                return RedirectToAction("Dashboard", "Patient", new { id = userId });

            return RedirectToAction(nameof(Index));
        }

        private bool CanAccessThread(UserType type, int userId, ChatThread thread)
        {
            if (type == UserType.Patient)
                return thread.PatientId == userId;

            if (type == UserType.Receptionist)
                return thread.ReceptionistId.HasValue && thread.ReceptionistId.Value == userId;

            return false;
        }

        private int? ResolveReceiverUserId(UserType type, ChatThread thread)
        {
            if (type == UserType.Patient)
                return thread.ReceptionistId;

            if (type == UserType.Receptionist)
                return thread.PatientId;

            return null;
        }
    }
}
