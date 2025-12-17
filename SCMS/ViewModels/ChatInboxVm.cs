using System;
using System.Collections.Generic;

namespace SCMS.ViewModels
{
    public class ChatThreadRowVm
    {
        public int ThreadId { get; set; }

        public string PatientName { get; set; } = "";

        public DateTime CreatedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }

        public int UnreadCount { get; set; }

        // عشان نعرف دي taken ولا لسه unassigned
        public bool IsAssigned { get; set; }
    }

    public class ChatInboxVm
    {
        public List<ChatThreadRowVm> Unassigned { get; set; } = new();
        public List<ChatThreadRowVm> Mine { get; set; } = new();
    }
}
