using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WOS.ViewModels
{
    public class VM_user_skill
    {
        public string user_id { get; set; }
        public bool evaluation { get; set; }
        public string proc_code { get; set; }
        public string proc_name { get; set; }
        public string skill_type { get; set; }
    }
}