using System;
using System.Collections.Generic;

namespace SCMS.ViewModels
{
    public class ChatMessageVm
    {
        public int MessageId { get; set; }
        public string SenderName { get; set; } = null!;
        public bool IsFromCurrentUser { get; set; }
        public string Text { get; set; } = null!;
        public DateTime SentAt { get; set; }
    }

    public class ChatConversationVm
    {
        public int ConversationId { get; set; }

        public string AgentName { get; set; } = "Customer Care Supervisor";

        public IEnumerable<ChatMessageVm> Messages { get; set; }
            = new List<ChatMessageVm>();

        public string NewMessageText { get; set; } = "";
    }
}
