//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace WOS.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class tr_main_job
    {
        public string main_job_no { get; set; }
        public string wo_no { get; set; }
        public Nullable<int> customer_oid { get; set; }
        public string customer_name { get; set; }
        public string cost_code { get; set; }
        public Nullable<int> plant_id { get; set; }
        public string plant_code { get; set; }
        public string plant_div_name { get; set; }
        public string item { get; set; }
        public string job_type { get; set; }
        public string product_type_code { get; set; }
        public string product_type_name { get; set; }
        public string description { get; set; }
        public Nullable<int> qty { get; set; }
        public string unit { get; set; }
        public string ref_old_td { get; set; }
        public string wo_evaluation { get; set; }
        public string due_date { get; set; }
        public string due_time { get; set; }
        public string promise_date { get; set; }
        public string promise_time { get; set; }
        public string entry_date { get; set; }
        public string entry_time { get; set; }
        public string update_date { get; set; }
        public string update_time { get; set; }
    }
}