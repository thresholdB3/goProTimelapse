using System;

namespace GoProTimelapse
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string FirstName { get; set; } 
        public string LastName { get; set; }
        public bool IsAdmin { get; set; } = false;
        public DateTime RegisteredAt { get; set; }
        public bool SunsetSubscribtion { get; set; } = false;

        public long TGUserId { get; set; }
    }
}

