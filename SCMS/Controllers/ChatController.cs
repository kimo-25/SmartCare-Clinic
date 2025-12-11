using Microsoft.AspNetCore.Mvc;
using SCMS.ViewModels;

namespace SCMS.Controllers
{
    public class ChatController : Controller
    {
        [HttpGet]
        public IActionResult Conversation(int? conversationId)
        {
            // لسه مفيش DB لل Chat, هنعرض مثال ثابت
            var vm = new ChatConversationVm
            {
                ConversationId = conversationId ?? 1,
                AgentName = "Customer Care Supervisor",
                Messages = new List<ChatMessageVm>
                {
                    new ChatMessageVm
                    {
                        MessageId = 1,
                        SenderName = "Raul Kim",
                        IsFromCurrentUser = false,
                        Text = "Hi there.",
                        SentAt = DateTime.Now.AddMinutes(-5)
                    },
                    new ChatMessageVm
                    {
                        MessageId = 2,
                        SenderName = "You",
                        IsFromCurrentUser = true,
                        Text = "Hello! I need help.",
                        SentAt = DateTime.Now.AddMinutes(-3)
                    }
                }
            };

            return View(vm);
        }

        [HttpPost]
        public IActionResult SendMessage(ChatConversationVm vm)
        {
            // هنا المفروض تحفظي الرسالة في DB
            // حالياً هنرجّع على نفس الـ Conversation
            return RedirectToAction(nameof(Conversation), new { conversationId = vm.ConversationId });
        }
    }
}
