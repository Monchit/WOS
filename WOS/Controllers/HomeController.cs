using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WOS.Models;
using WOS.ViewModels;
using System.Linq.Dynamic;   // JQGrid: VIP Linq
using MvcJqGrid;             // JQGrid
using System.Globalization;
using System.Text;
using Gma.QrCodeNet.Encoding;                   // QR Code
using Gma.QrCodeNet.Encoding.Windows.Render;    // QR Code
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Web.Script.Serialization;
using WebCommonFunction;

namespace WOS.Controllers
{
    public class HomeController : Controller
    {
        tooldietrainEntities dbTooldie = new tooldietrainEntities();
        TNCUtility util = new TNCUtility();
        TNCConversion conv = new TNCConversion();
        CultureInfo culture = new CultureInfo("ru-RU");
        DateTime dateformat, dateformat2;
        string datetime, datetime2;
        CultureInfo UsaCulture = new CultureInfo("en-US");

        public ActionResult WoStatus()
        {
            if (chkSession()) return RedirectToAction("Login", "Account");
            ViewBag.PlantDiv = dbTooldie.tr_main_job.GroupBy(g => g.plant_div_name).Select(s => s.Key);

            return View();
        }

        [OutputCache(Duration = 0, VaryByParam = "*", NoStore = true)]
        public ActionResult GetWoStatus(string txtTDNo, string txtMainJob, string selProductType, string txtOldTD, string txtMainWO, string txtDateFrom, string txtDateTo)
        {

            var query = (from b in dbTooldie.tr_main_job
                         join s in dbTooldie.tr_sub_job
                         on b.main_job_no equals s.main_job_no
                         join a in dbTooldie.tr_job_progress
                         on new { b.main_job_no, s.sub_job_no } equals new { a.main_job_no, a.sub_job_no } //into r
                         //from a in r.DefaultIfEmpty()
                         join c in dbTooldie.tr_process on a.process_code.Trim() equals c.proc_code.Trim() into p
                         from c in p.DefaultIfEmpty()
                         join g in (dbTooldie.td_job_progress.GroupBy(g => new { g.main_job_no, g.sub_job_no, g.marking_step }).Select(s => new { s.Key.main_job_no, s.Key.sub_job_no, marking_step = s.Key.marking_step.Value, process_status = s.Max(m => m.process_status), start_date = s.Min(m => m.start_date) })) on new { a.main_job_no, a.sub_job_no, a.marking_step } equals new { g.main_job_no, g.sub_job_no, g.marking_step } into q
                         from g in q.DefaultIfEmpty()
                         select new
                         {
                             b.main_job_no,
                             b.ref_old_td,
                             b.entry_date,
                             s.sub_job_no,
                             s.ref_sub_workorder,
                             b.wo_no,
                             b.product_type_name,
                             a.marking_step,
                             c.proc_name,
                             g.process_status,
                             g.start_date
                         }).GroupBy(g => new { g.main_job_no, g.sub_job_no, g.ref_old_td, g.ref_sub_workorder, g.wo_no, g.product_type_name, g.entry_date })
                         .Select(s => new
                         {
                             s.Key.main_job_no,
                             s.Key.ref_old_td,
                             s.Key.sub_job_no,
                             s.Key.ref_sub_workorder,
                             s.Key.wo_no,
                             s.Key.product_type_name,
                             s.Key.entry_date,
                             //percent = (s.Where(w => w.process_status != null && w.process_status.Trim() == "Y").Count() / (double)s.Count()) * 100,
                             process = s.Select(se => new
                             {
                                 se.marking_step,
                                 se.proc_name,
                                 se.process_status,
                                 se.start_date
                             }).OrderBy(o => o.marking_step)
                         });


            if (!string.IsNullOrEmpty(txtMainJob))
            {
                query = query.Where(w => w.main_job_no.Trim() == txtMainJob.Trim().ToUpper());
            }

            if (!string.IsNullOrEmpty(selProductType))
            {
                query = query.Where(w => w.product_type_name.Trim() == selProductType.Trim());
            }

            if (!string.IsNullOrEmpty(txtMainWO))
            {
                query = query.Where(w => w.wo_no.Trim() == txtMainWO.Trim().ToUpper());
            }

            if (!string.IsNullOrEmpty(txtOldTD))
            {
                query = query.Where(w => w.ref_old_td.Trim() == txtOldTD.Trim().ToUpper());
            }

            if (!string.IsNullOrEmpty(txtDateFrom) && !string.IsNullOrEmpty(txtDateTo))
            {
                var from = ToPGDateFormat(txtDateFrom);
                var to = ToPGDateFormat(txtDateTo);
                query = query.Where(w => w.entry_date.CompareTo(from) >= 0 && w.entry_date.CompareTo(to) <= 0);
            }

            return Json(query, JsonRequestBehavior.AllowGet);

            //var job = dbTooldie.tr_main_job.AsQueryable();

            //if (!string.IsNullOrEmpty(txtMainJob))
            //{
            //    job = job.Where(w => w.main_job_no.Trim() == txtMainJob.Trim().ToUpper());
            //}

            //if (!string.IsNullOrEmpty(selProductType))
            //{
            //    job = job.Where(w => w.product_type_name.Trim() == selProductType.Trim());
            //}

            //if (!string.IsNullOrEmpty(txtMainWO))
            //{
            //    job = job.Where(w => w.wo_no.Trim() == txtMainWO.Trim());
            //}

            //if (!string.IsNullOrEmpty(txtOldTD))
            //{
            //    job = job.Where(w => w.ref_old_td.Trim() == txtOldTD.Trim());
            //}

            
            //var query = (from a in job
            //             join s in dbTooldie.tr_sub_job
            //             on a.main_job_no equals s.main_job_no
            //             select new
            //             {
            //                 a.main_job_no,
            //                 s.sub_job_no,
            //                 s.ref_sub_workorder
            //             }).OrderBy(o => o.main_job_no).ThenBy(o => o.sub_job_no);

            //if (query.Any())
            //{
            //    return Json(query, JsonRequestBehavior.AllowGet);
            //}
            //else
            //{
            //    return Json(0, JsonRequestBehavior.AllowGet);
            //}
        }

        public ActionResult GetMasterProcess(string mainJob, string subJob)
        {
            var todayInt = int.Parse((DateTime.Now.Year) + DateTime.Now.ToString("MMdd"));

            var query = (from a in dbTooldie.tr_job_progress
                        .Where(w => w.main_job_no.Trim() == mainJob.Trim() && w.sub_job_no.Trim() == subJob.Trim())
                         join c in dbTooldie.tr_process on a.process_code.Trim() equals c.proc_code.Trim() into p
                         from c in p.DefaultIfEmpty()
                         join g in (dbTooldie.td_job_progress.GroupBy(g => new { g.main_job_no, g.sub_job_no, g.marking_step }).Select(s => new { s.Key.main_job_no, s.Key.sub_job_no, marking_step = s.Key.marking_step.Value, process_status = s.Max(m => m.process_status), start_date = s.Min(m => m.start_date) })) on new { a.main_job_no, a.sub_job_no, a.marking_step } equals new { g.main_job_no, g.sub_job_no, g.marking_step } into q
                         from g in q.DefaultIfEmpty()
                         select new
                         {
                             a.marking_step,
                             c.proc_name,
                             g.process_status,
                             g.start_date
                         }).AsEnumerable().Select(s => new
                         {
                             s.marking_step,
                             s.proc_name,
                             s.process_status,
                             s.start_date,
                             danger = (s.process_status.Trim() == "D" && (s.start_date == null ? 0 : todayInt - int.Parse(s.start_date)) > 5) ? true : false
                         }).OrderBy(o => o.marking_step);

            return Json(query, JsonRequestBehavior.AllowGet);
        }

        public double GetExportProgressive(string mainjob, string subjob)
        {
            var total = (from b in dbTooldie.tr_job_progress
                         where b.main_job_no.Trim() == mainjob.Trim() && b.sub_job_no.Trim() == subjob.Trim()
                         select b).Count();
            var comp = (from a in dbTooldie.td_job_progress
                        where a.main_job_no.Trim() == mainjob.Trim() && a.sub_job_no.Trim() == subjob.Trim()
                        && a.process_status.Trim() == "Y"
                        group a by a.marking_step into g
                        select g.Key).Count();

            double percent;

            if (comp <= total) percent = (comp / (double)total) * 100;
            else percent = 100;

            return percent;
        }

        public void ExportWOStatus(string txtTDNo, string txtMainJob, string selProductType, string txtOldTD, string txtMainWO, string txtDateFrom, string txtDateTo)
        {
            var job = dbTooldie.tr_main_job.AsQueryable();

            if (!string.IsNullOrEmpty(txtMainJob))
            {
                job = job.Where(w => w.main_job_no.Trim() == txtMainJob.Trim());
            }

            if (!string.IsNullOrEmpty(selProductType))
            {
                job = job.Where(w => w.product_type_name.Trim() == selProductType.Trim());
            }

            if (!string.IsNullOrEmpty(txtDateFrom) && !string.IsNullOrEmpty(txtDateTo))
            {
                var from = ToPGDateFormat(txtDateFrom);
                var to = ToPGDateFormat(txtDateTo);
                job = job.Where(w => w.entry_date.CompareTo(from) >= 0 && w.entry_date.CompareTo(to) <= 0);
            }

            if (!string.IsNullOrEmpty(txtMainWO))
            {
                job = job.Where(w => w.wo_no.Trim() == txtMainWO.Trim());
            }

            if (!string.IsNullOrEmpty(txtOldTD))
            {
                job = job.Where(w => w.ref_old_td.Trim() == txtOldTD.Trim());
            }

            var query = (from a in job
                         join s in dbTooldie.tr_sub_job
                         on a.main_job_no equals s.main_job_no
                         join b in dbTooldie.tm_rack_part
                         on new { a.main_job_no, s.sub_job_no } equals new { b.main_job_no, b.sub_job_no } into g
                         from b in g.DefaultIfEmpty()
                         select new
                         {
                             a.main_job_no,
                             s.sub_job_no,
                             s.ref_sub_workorder,
                             a.product_type_name
                         }).OrderBy(o => o.main_job_no).ThenBy(o => o.sub_job_no);

            var maxProcessCount = 0;
            var data = new List<object[]>();

            //var todayInt = int.Parse((DateTime.Now.Year) + DateTime.Now.ToString("MMdd"));
            foreach (var item in query)
            {

                // Progressive
                var progressive = GetExportProgressive(item.main_job_no, item.sub_job_no);
                // Process Master
                var processMaster = (from a in dbTooldie.tr_job_progress
                        .Where(w => w.main_job_no.Trim() == item.main_job_no.Trim() && w.sub_job_no.Trim() == item.sub_job_no.Trim())
                                     join c in dbTooldie.tr_process on a.process_code.Trim() equals c.proc_code.Trim() into p
                                     from c in p.DefaultIfEmpty()
                                     //join g in (dbTooldie.td_job_progress.GroupBy(g => new { g.main_job_no, g.sub_job_no, g.marking_step }).Select(s => new { s.Key.main_job_no, s.Key.sub_job_no, marking_step = s.Key.marking_step.Value, process_status = s.Max(m => m.process_status), start_date = s.Min(m => m.start_date) })) on new { a.main_job_no, a.sub_job_no, a.marking_step } equals new { g.main_job_no, g.sub_job_no, g.marking_step } into q
                                     join g in (dbTooldie.td_job_progress.GroupBy(g => new { g.main_job_no, g.sub_job_no, g.marking_step }).Select(s => new { s.Key.main_job_no, s.Key.sub_job_no, marking_step = s.Key.marking_step.Value, process_status = s.Max(m => m.process_status) })) on new { a.main_job_no, a.sub_job_no, a.marking_step } equals new { g.main_job_no, g.sub_job_no, g.marking_step } into q
                                     from g in q.DefaultIfEmpty()
                                     select new
                                     {
                                         a.marking_step,
                                         c.proc_name,
                                         g.process_status//,
                                         //g.start_date
                                     }).OrderBy(o => o.marking_step).AsEnumerable();

                if (processMaster.Count() > maxProcessCount)
                {
                    maxProcessCount = processMaster.Count();
                }
                var row = new List<object>();
                row.Add(item.main_job_no.Trim());
                row.Add(item.sub_job_no.Trim());
                row.Add(item.ref_sub_workorder.Trim());
                row.Add(item.product_type_name);
                row.Add(processMaster.Count());
                row.Add(progressive > 0 ? Math.Round(progressive, 2) + "%" : "0.00%");


                foreach (var process in processMaster)
                {
                    //row.Add((string.IsNullOrEmpty(process.process_status) ? "N- " : ((process.start_date != null && process.process_status.Trim() == "D" && todayInt - int.Parse(process.start_date) > 5) ? "Z- " : process.process_status.Trim() + "- ")) + process.proc_name.Trim());
                    row.Add((string.IsNullOrEmpty(process.process_status) ? "N- " : process.process_status.Trim() + "- ") + process.proc_name.Trim());
                }

                data.Add(row.ToArray());
            }


            //var query = (from b in dbTooldie.tr_main_job
            //             join s in dbTooldie.tr_sub_job
            //             on b.main_job_no equals s.main_job_no
            //             join a in dbTooldie.tr_job_progress
            //             on new { b.main_job_no, s.sub_job_no } equals new { a.main_job_no, a.sub_job_no } //into r
            //             //from a in r.DefaultIfEmpty()
            //             join c in dbTooldie.tr_process on a.process_code.Trim() equals c.proc_code.Trim() into p
            //             from c in p.DefaultIfEmpty()
            //             join g in (dbTooldie.td_job_progress.GroupBy(g => new { g.main_job_no, g.sub_job_no, g.marking_step }).Select(s => new { s.Key.main_job_no, s.Key.sub_job_no, marking_step = s.Key.marking_step.Value, process_status = s.Max(m => m.process_status), start_date = s.Min(m => m.start_date) })) on new { a.main_job_no, a.sub_job_no, a.marking_step } equals new { g.main_job_no, g.sub_job_no, g.marking_step } into q
            //             from g in q.DefaultIfEmpty()
            //             select new
            //             {
            //                 b.main_job_no,
            //                 b.ref_old_td,
            //                 s.sub_job_no,
            //                 s.ref_sub_workorder,
            //                 b.wo_no,
            //                 b.product_type_name,
            //                 a.marking_step,
            //                 c.proc_name,
            //                 g.process_status,
            //                 g.start_date,
            //                 a.entry_date
            //             }).GroupBy(g => new { g.main_job_no, g.sub_job_no, g.ref_old_td, g.ref_sub_workorder, g.wo_no, g.product_type_name })
            //             .Select(s => new {
            //                 s.Key.main_job_no,
            //                 s.Key.ref_old_td,
            //                 s.Key.sub_job_no,
            //                 s.Key.ref_sub_workorder,
            //                 s.Key.wo_no,
            //                 s.Key.product_type_name,
            //                 begin_date = s.Min(m => m.entry_date),
            //                 process = s.Select(se => new {
            //                     se.marking_step,
            //                     se.proc_name,
            //                     se.process_status,
            //                     se.start_date,
            //                     se.entry_date
            //                 }).OrderBy(o => o.marking_step)
            //             });


            //if (!string.IsNullOrEmpty(txtMainJob))
            //{
            //    query = query.Where(w => w.main_job_no.Trim() == txtMainJob.Trim().ToUpper());
            //}

            //if (!string.IsNullOrEmpty(selProductType))
            //{
            //    query = query.Where(w => w.product_type_name.Trim() == selProductType.Trim());
            //}

            //if (!string.IsNullOrEmpty(txtMainWO))
            //{
            //    query = query.Where(w => w.wo_no.Trim() == txtMainWO.Trim());
            //}

            //if (!string.IsNullOrEmpty(txtOldTD))
            //{
            //    query = query.Where(w => w.ref_old_td.Trim() == txtOldTD.Trim());
            //}

            //if (!string.IsNullOrEmpty(txtDateFrom) && !string.IsNullOrEmpty(txtDateTo))
            //{
            //    var from = InputDateToDBStrDate(txtDateFrom);
            //    var to = InputDateToDBStrDate(txtDateTo);
            //    query = query.Where(w => w.begin_date.CompareTo(from) >= 0 && w.begin_date.CompareTo(to) <= 0);
            //}



            //var maxProcessCount = 0;
            //var data = new List<object[]>();
            //var todayInt = int.Parse((DateTime.Now.Year) + DateTime.Now.ToString("MMdd"));

            //foreach (var item in query)
            //{
            //    var row = new List<object>();
            //    row.Add(item.main_job_no.Trim());
            //    row.Add(item.sub_job_no.Trim());
            //    row.Add(item.ref_sub_workorder.Trim());
            //    row.Add(item.process.Select(s => s.marking_step).Count());

            //    var progressive = (item.process.Where(w => w.process_status != null && w.process_status.Trim() == "Y").Select(s => s.marking_step).Count() / (double)item.process.Select(s => s.marking_step).Count()) * 100;
            //    row.Add(progressive > 0 ? /*(processMaster.Where(w => w.start_date != null && w.process_status.Trim() == "D" && todayInt - int.Parse(w.start_date) > 5).Count() > 0 ? "Z-" : "") +*/ Math.Round(progressive, 2) + "%" : "0.00%");

            //    foreach (var process in item.process.OrderBy(o => o.marking_step))
            //    {
            //        row.Add((string.IsNullOrEmpty(process.process_status) ? "N- " : ((process.start_date != null && process.process_status.Trim() == "D" && todayInt - int.Parse(process.start_date) > 5) ? "Z- " : process.process_status.Trim() + "- ")) + process.proc_name.Trim());

            //    }

            //    data.Add(row.ToArray());
            //    maxProcessCount = item.process.Count() > maxProcessCount ? item.process.Count() : maxProcessCount;
            //}


            // Header
            var header = new List<string>() { "Main Job", "Sub Job", "Ref. Sub W/O", "Type", "Total Process", "Progressive" };
            for (int i = 1; i <= maxProcessCount; i++)
            {
                header.Add("Process" + i);
            }


            util.ModifyExcel("Stuff", "BogyTemplate.xlsx", null, data.AsEnumerable(), header);
        }

        private string InputDateToDBStrDate(string inputDate){
            var arr = inputDate.Split('/');
            return arr[2] + arr[1] + arr[0];
        }

        public ActionResult GetSubWO(string term)
        {
            var subWO = dbTooldie.tr_sub_job.Where(w => w.ref_sub_workorder.StartsWith(term)).Take(12).Select(s => s.ref_sub_workorder.Trim());
            return Json(subWO, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetMainJobNo(string refSubWO)
        {
            var mainJobNo = dbTooldie.tr_sub_job.Where(w => w.ref_sub_workorder == refSubWO).FirstOrDefault();
            if (mainJobNo != null)
            {
                return Json(mainJobNo.main_job_no.Trim(), JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(0, JsonRequestBehavior.AllowGet);
            }
        }


        public bool chkSession()
        {
            if (Session["userLogin"] == null)
                return true;
            else
                return false;
        }

        public ActionResult Index()
        {
            if (chkSession()) return RedirectToAction("Login", "Account");

            //ViewBag.Message = "Modify this template to jump-start your ASP.NET MVC application.";

            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your app description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public ActionResult Chk_WO(string mainjob, string subjob)
        {
            if (chkSession()) return RedirectToAction("Login", "Account");

            if (!string.IsNullOrEmpty(mainjob))
            {
                ViewBag.MainJob = mainjob;
                //var query = from a in dbTooldie.tr_sub_job
                //            where a.main_job_no.Trim() == mainjob.Trim()
                //            select a.sub_job_no.Trim();
                //if (query.Any())
                //{
                //    ViewBag.SubJobList = query;
                //}
            }
            if (!string.IsNullOrEmpty(subjob))
            {
                ViewBag.SelSubJob = subjob;
            }
            return View();
        }

        [HttpPost]
        public void ExportToXLS()
        {
            var date = Request.Form["datepicker"] == null ? "" : Request.Form["datepicker"];
            var dateto = Request.Form["date_to"] == null ? "" : Request.Form["date_to"];
            string shift = (Request.Form.GetValues("chk")) == null ? "" : (Request.Form.GetValues("chk")[0]);
            //var qty = Request.Form["txtQty"];
            string pdt = Request.Form["selPdT"];
            string process = Request.Form["DropDown_Proc"];
            string status = Request.Form["selStatus"];

            dateformat = conv.DateDisplayToDB(date);
            dateformat2 = !string.IsNullOrEmpty(dateto) ? conv.DateDisplayToDB(dateto) : dateformat;

            datetime = dateformat.ToString("yyyyMMdd", UsaCulture);
            datetime2 = dateformat2.ToString("yyyyMMdd", UsaCulture);

            var shiftTimeFrom = "070000";
            var shiftTimeTo = "190000";
            var changeDayA = "000000";
            var changeDayB = "235959";

            var db = (from a in dbTooldie.td_job_progress
                      join b in dbTooldie.tr_main_job on a.main_job_no equals b.main_job_no into group1
                      from b in group1.DefaultIfEmpty()
                      join c in dbTooldie.tr_process on a.process_code equals c.proc_code into group2
                      from c in group2.DefaultIfEmpty()
                      join d in dbTooldie.tr_machine on new { x = a.machine_no, y = a.process_code } equals new { x = d.machine_code, y = d.proc_code } into q
                      from d in q.DefaultIfEmpty()
                      join e in dbTooldie.tr_sub_job on new { a.main_job_no, a.sub_job_no } equals new { e.main_job_no, e.sub_job_no } into g
                      from e in g.DefaultIfEmpty()
                      //join f in dbTooldie.td_comment on new { a.main_job_no, a.sub_job_no, a.marking_step.Value } equals new { f.main_job_no, f.sub_job_no, f.marking_step } into cm
                      //from f in cm.DefaultIfEmpty()
                      select new
                      {
                          a.main_job_no,
                          b.ref_old_td,
                          a.sub_job_no,
                          e.ref_sub_workorder,
                          b.item,
                          type_est = !string.IsNullOrEmpty(a.type_est) ? (a.type_est.Trim() == "S" ? "SETUP" : "MACHINE") : "",
                          process_status = !string.IsNullOrEmpty(a.process_status) ? a.process_status : "N",
                          b.product_type_name,
                          a.qty,
                          a.process_code,
                          c.proc_name,
                          machine_name = !string.IsNullOrEmpty(d.machine_name) ? d.machine_name : "",
                          a.start_date,
                          a.start_time,
                          a.start_user_name,
                          a.finished_date,
                          a.finished_time,
                          a.finished_user_name,
                          a.marking_step,
                          b.job_type
                      }).OrderBy(o => o.main_job_no).Where(x => x.start_date.Trim().CompareTo(datetime) >= 0 && x.start_date.Trim().CompareTo(datetime2) <= 0);

            if (shift == "1")          // Day Shift
            {
                db = db.Where(x => (x.start_date.Trim().CompareTo(datetime) >= 0 && x.start_date.Trim().CompareTo(datetime2) <= 0)
                    && (x.start_time.Trim().CompareTo(shiftTimeFrom) >= 0 && x.finished_time.Trim().CompareTo(shiftTimeTo) <= 0));
            }
            else if (shift == "2")     // Night Shift
            {
                db = db.Where(x => ((x.start_date.Trim().CompareTo(datetime) >= 0 && x.start_date.Trim().CompareTo(datetime2) <= 0)
                    && (x.start_time.Trim().CompareTo(changeDayA) >= 0 && x.finished_time.Trim().CompareTo(shiftTimeFrom) <= 0))
                    || ((x.start_date.Trim().CompareTo(datetime) >= 0 && x.start_date.Trim().CompareTo(datetime2) <= 0)
                    && (x.start_time.Trim().CompareTo(shiftTimeTo) >= 0 && x.finished_time.Trim().CompareTo(changeDayB) <= 0)));
            }

            //if (!string.IsNullOrEmpty(qty))
            //{
            //    int x = int.Parse(qty);
            //    db = db.Where(w => w.qty == x);
            //}
            if (!string.IsNullOrEmpty(pdt))
            {
                db = db.Where(w => w.product_type_name.Trim() == pdt);
                //db = db.Where(w => w.product_type_name.ToLower().Contains(pdt.ToLower()));
            }
            if (!string.IsNullOrEmpty(process))
            {
                db = db.Where(w => w.process_code.Trim() == process.Trim());
            }
            if (!string.IsNullOrEmpty(status))
            {
                db = db.Where(w => w.process_status.Trim() == status);
            }

            var output = db.ToList()
                    .Select(s => new
                    {
                        MainJobNo = s.main_job_no == null ? "" : s.main_job_no.Trim(),
                        MainRef = s.ref_old_td == null ? "" : s.ref_old_td.Trim(),
                        SubJob = s.sub_job_no == null ? "" : s.sub_job_no.Trim(),
                        RefSubWO = s.ref_sub_workorder == null ? "" : s.ref_sub_workorder.Trim(),
                        ItemCode = s.item == null ? "" : s.item.Trim(),
                        TypeEst = s.type_est,
                        Status = s.process_status,
                        YType = s.product_type_name == null ? "" : s.product_type_name.Trim(),
                        JType = s.job_type == "0" ? "New" : "Repair",//Update 2015-06-29 by Monchit W.
                        Qty = s.qty,
                        Process = s.proc_name == null ? "" : s.proc_name.Trim(),
                        Machine = s.machine_name == null ? "" : s.machine_name.Trim(),
                        StartDate = NewDateFormat(s.start_date),
                        StartTime = NewTimeFormat(s.start_time),
                        FinishDate = !string.IsNullOrEmpty(s.finished_date.Trim()) ? NewDateFormat(s.finished_date.Trim()) : "",
                        FinishTime = !string.IsNullOrEmpty(s.finished_time.Trim()) ? NewTimeFormat(s.finished_time.Trim()) : "",
                        SpendTime = !string.IsNullOrEmpty(s.finished_date.Trim()) && !string.IsNullOrEmpty(s.finished_time.Trim()) ?
                        CalSpendTimetoMinute(NewDateFormat(s.start_date), NewTimeFormat(s.start_time), NewDateFormat(s.finished_date.Trim()), NewTimeFormat(s.finished_time.Trim())) : 0,
                        StartBy = s.start_user_name == null ? "" : s.start_user_name.Trim(),
                        FinishBy = s.finished_user_name == null ? "" : s.finished_user_name.Trim(),
                        Comment = s.marking_step != null ? GetComment(s.main_job_no, s.sub_job_no, s.marking_step) : ""
                    }).ToList();

            TNCUtility util = new TNCUtility();
            util.CreateExcel(output, "DailyScan_" + DateTime.Now.ToString("yyyy-MM-dd"));
        }

        private string GetComment(string main, string sub, int? step)
        {
            var query = (from a in dbTooldie.td_comment
                         where a.main_job_no == main && a.sub_job_no == sub && a.marking_step == step
                         select a).FirstOrDefault();
            if (query != null)
            {
                return query.comment_process;
            }
            else
            {
                return "";
            }
        }

        public ActionResult Daily_ScanReport()
        {

            if (chkSession()) return RedirectToAction("Login", "Account");

            ViewBag.process = (from a in dbTooldie.tr_process
                               select a).OrderBy(o => o.proc_name).Distinct();

            return View();
        }

        public ActionResult Process_Time()
        {
            if (chkSession()) return RedirectToAction("Login", "Account");

            ViewBag.process = (from a in dbTooldie.tr_process
                               select a).OrderBy(o => o.proc_name).Distinct();

            return View();
        }

        public ActionResult CreateGridDaily(string sidx, string sord, int page, int rows, string shift, string date, string dateto, string pdt, string process, string status)
        {
            // Convert format from dd/MM/yyyy to yyyyMMdd
            
            //dateformat = Convert.ToDateTime(date, culture);
            dateformat = conv.DateDisplayToDB(date);
            //dateformat2 = dateformat.AddDays(1);
            dateformat2 = !string.IsNullOrEmpty(dateto) ? conv.DateDisplayToDB(dateto) : dateformat;

            datetime = dateformat.ToString("yyyyMMdd", UsaCulture);
            datetime2 = dateformat2.ToString("yyyyMMdd", UsaCulture);

            var shiftTimeFrom = "070000";
            var shiftTimeTo = "190000";
            var changeDayA = "000000";
            var changeDayB = "235959";
            var test = datetime.CompareTo(datetime2);
            var db = (from a in dbTooldie.td_job_progress
                      join b in dbTooldie.tr_main_job on a.main_job_no equals b.main_job_no into group1
                      from b in group1.DefaultIfEmpty()
                      join c in dbTooldie.tr_process on a.process_code equals c.proc_code into group2
                      from c in group2.DefaultIfEmpty()
                      join d in dbTooldie.tr_machine on new { x = a.machine_no, y = a.process_code } equals new { x = d.machine_code, y = d.proc_code } into q
                      from d in q.DefaultIfEmpty()
                      join e in dbTooldie.tr_sub_job on new { a.main_job_no, a.sub_job_no } equals new { e.main_job_no, e.sub_job_no } into g
                      from e in g.DefaultIfEmpty()
                      select new
                      {
                          a.main_job_no,
                          b.ref_old_td,
                          a.sub_job_no,
                          e.ref_sub_workorder,
                          b.item,
                          process_status = !string.IsNullOrEmpty(a.process_status) ? a.process_status : "N",
                          type_est = !string.IsNullOrEmpty(a.type_est) ? (a.type_est.Trim() == "S" ? "SETUP" : "MACHINE") : "",
                          c.proc_name,
                          machine_name = !string.IsNullOrEmpty(d.machine_name) ? d.machine_name : "",
                          a.start_date,
                          a.start_time,
                          a.finished_date,
                          a.finished_time,
                          a.start_user_name,
                          //finished_date = !string.IsNullOrEmpty(a.finished_date) ? a.finished_date : "",
                          //finished_time = !string.IsNullOrEmpty(a.finished_time) ? a.finished_time : "",
                          a.finished_user_name,
                          b.product_type_name,
                          a.process_code,
                          a.qty
                      }).Where(x => x.start_date.Trim().CompareTo(datetime) >= 0 && x.start_date.Trim().CompareTo(datetime2) <= 0);

            if (shift == "1")          // Day Shift
            {
                db = db.Where(x => (x.start_date.Trim().CompareTo(datetime) >= 0 && x.start_date.Trim().CompareTo(datetime2) <= 0) 
                    && (x.start_time.Trim().CompareTo(shiftTimeFrom) >= 0 && x.finished_time.Trim().CompareTo(shiftTimeTo) <= 0));
            }
            else if (shift == "2")     // Night Shift
            {
                db = db.Where(x => ((x.start_date.Trim().CompareTo(datetime) >= 0 && x.start_date.Trim().CompareTo(datetime2) <= 0) 
                    && (x.start_time.Trim().CompareTo(changeDayA) >= 0 && x.finished_time.Trim().CompareTo(shiftTimeFrom) <= 0))
                    || ((x.start_date.Trim().CompareTo(datetime) >= 0 && x.start_date.Trim().CompareTo(datetime2) <= 0) 
                    && (x.start_time.Trim().CompareTo(shiftTimeTo) >= 0 && x.finished_time.Trim().CompareTo(changeDayB) <= 0)));
            }
            //if (!string.IsNullOrEmpty(qty))
            //{
            //    int x = int.Parse(qty);
            //    db = db.Where(w => w.qty == x);
            //}
            if (!string.IsNullOrEmpty(pdt))
            {
                db = db.Where(w => w.product_type_name.Trim() == pdt);
                //db = db.Where(w => w.product_type_name.ToLower().Contains(pdt.ToLower()));
            }
            if (!string.IsNullOrEmpty(process))
            {
                db = db.Where(w => w.process_code.Trim() == process.Trim());
            }
            if (!string.IsNullOrEmpty(status))
            {
                db = db.Where(w => w.process_status.Trim() == status);
            }
            
            var pageIndex = Convert.ToInt32(page) - 1;
            var pageSize = rows;
            var totalRecords = db.Count();
            var totalPages = (int)Math.Ceiling(totalRecords / (float)pageSize);

            //TempData["totalQty"] = db.Sum(w => w.qty);
            var sumqty = db.Sum(w => w.qty);

            // This is possible because I'm using the LINQ Dynamic Query Library
            var store = db.AsQueryable()
                    .OrderBy(sidx + " " + sord)
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize);

            var jsonData = new
            {
                sum = sumqty,
                total = totalPages,
                page = page,
                records = totalRecords,
                rows = store.ToList().Select(q => new
                {
                    i = q.main_job_no,
                    cell = new object[] { 
                        q.main_job_no.Trim(),
                        //q.ref_old_td.Trim(),
                        q.sub_job_no.Trim(),
                        //q.ref_sub_workorder.Trim(),
                        q.item.Trim(),
                        q.type_est,
                        q.process_status,
                        q.proc_name.Trim(),
                        q.machine_name.Trim(),
                        //q.qty,
                        //q.start_date,
                        NewTimeFormat(q.start_time),
                        NewTimeFormat(q.finished_time),
                        //spend_time = CalSpendTime(q.start_date, q.start_time, q.finished_date, q.finished_time),
                        CalSpendTimetoMinute(NewDateFormat(q.start_date),NewTimeFormat(q.start_time),
                        NewDateFormat(q.finished_date),NewTimeFormat(q.finished_time)),
                        NewDateFormat(q.finished_date),
                        q.start_user_name,
                        q.finished_user_name
                    }

                }).ToArray()
            };

            return Json(jsonData);
        }

        public JsonResult GetDetails(string JobNo, string WO)
        {
            var details = from i in dbTooldie.tr_main_job
                          select i;

            if ((!string.IsNullOrEmpty(JobNo)) || (string.IsNullOrEmpty(WO)))
            {
                details = details.Where(x => x.main_job_no.Trim() == JobNo.Trim());
            }
            else if ((string.IsNullOrEmpty(JobNo)) || (!string.IsNullOrEmpty(WO)))
            {
                details = details.Where(x => x.wo_no.Trim() == WO.Trim());
            }
            else
            {
                details = details.Where(x => x.main_job_no.Trim() == JobNo.Trim() && x.wo_no.Trim() == WO.Trim());
            }

            return Json(details, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetWO_Evaluation(string JobNo, string WO)
        {
            string i;
            var result = "";

            var wo_eva = (from j in dbTooldie.tr_main_job
                          select j);
            //where j.main_job_no.Trim() == JobNo.Trim()
            //select j).Select(x => x.wo_evaluation).FirstOrDefault();

            if ((!string.IsNullOrEmpty(JobNo)) || (string.IsNullOrEmpty(WO)))
            {
                wo_eva = wo_eva.Where(a => a.main_job_no.Trim() == JobNo.Trim());
                result = wo_eva.Select(x => x.wo_evaluation).FirstOrDefault();

                if ((result != null) && (result.ToLower().IndexOf("i", 0, 1) >= 0))
                {
                    i = "1";
                }
                else if ((result != null) && (result.ToLower().IndexOf("o", 0, 1) >= 0))
                {
                    i = "2";
                }
                else
                {
                    i = "3";
                }
            }
            else if ((string.IsNullOrEmpty(JobNo)) || (!string.IsNullOrEmpty(WO)))
            {
                wo_eva = wo_eva.Where(a => a.wo_no.Trim() == WO.Trim());
                result = wo_eva.Select(x => x.wo_evaluation).FirstOrDefault();

                if ((result != null) && (result.ToLower().IndexOf("i", 0, 1) >= 0))
                {
                    i = "1";
                }
                else if ((result != null) && (result.ToLower().IndexOf("o", 0, 1) >= 0))
                {
                    i = "2";
                }
                else
                {
                    i = "3";
                }
            }
            else
            {
                wo_eva = wo_eva.Where(a => a.main_job_no.Trim() == JobNo.Trim() && a.wo_no.Trim() == WO.Trim());
                result = wo_eva.Select(x => x.wo_evaluation).FirstOrDefault();

                if ((result != null) && (result.ToLower().IndexOf("i", 0, 1) >= 0))
                {
                    i = "1";
                }
                else if ((result != null) && (result.ToLower().IndexOf("o", 0, 1) >= 0))
                {
                    i = "2";
                }
                else
                {
                    i = "3";
                }
            }

            return Json(i, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetProcessCode(string ProcessName)
        {
            string process_code = null;
            string racknum = null;
            string runrack = null;
            var format_rackpart = "RP";

            var db = (from a in dbTooldie.tm_rack_process
                      join max in
                          (
                              from b in dbTooldie.tm_rack_process
                              group b by new
                              {
                                  b.process_code
                              } into g
                              select new
                              {
                                  rack = g.Max(m => m.rack_no),

                              }) on a.rack_no.Trim() equals max.rack.Trim()

                      join process_name in dbTooldie.tr_process on a.process_code.Trim() equals process_name.proc_code.Trim() into group1
                      from process_name in group1.DefaultIfEmpty()

                      select new
                      {
                          a.rack_no,
                          a.process_code,
                          process_name.proc_name

                      }).Where(x => x.proc_name.Trim() == ProcessName.Trim()).FirstOrDefault();

            if (db != null && ProcessName != "")
            {
                // prefix(6 digits) of format rack no. : RP + process code
                process_code = format_rackpart + db.process_code.Substring(2, 4);

                // suffix(6 digits) of format rack no. : xxxxxx 
                racknum = db.rack_no.Substring(6, 6);
                runrack = string.Format("{0:000000}", Convert.ToInt32(racknum) + 1);
                
            }
            else if (db == null && ProcessName != "")
            {
                var proc_code = (from a in dbTooldie.tr_process
                                 select a).Where(x => x.proc_name.Trim() == ProcessName.Trim()).FirstOrDefault();

                // prefix(6 digits) of format rack no. : RP + process code
                process_code = format_rackpart + proc_code.proc_code.Substring(2, 4);

                // suffix(6 digits) of format rack no. : xxxxxx 
                runrack = string.Format("{0:000000}", 000001);             
            }

            List<object> arr = new List<object>();
            arr.Add(process_code);
            arr.Add(runrack);

            return Json(arr, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GridSubJob(string sidx, string sord, int page, int rows, string search, string job_no)
        {
            var db = (from tblSubJobList in
                          (
                                from a in dbTooldie.tr_job_progress
                                group a by new
                                {
                                    a.main_job_no,
                                    a.sub_job_no,
                                    
                                } into g
                                select new
                                {
                                    g.Key.main_job_no,
                                    g.Key.sub_job_no,
                                   
                                })

                      join td_progress in
                          (
                              from b in dbTooldie.td_job_progress
                              join max in
                                  (
                                    from c in dbTooldie.td_job_progress
                                    group c by new
                                    {
                                        c.main_job_no,
                                        c.sub_job_no
                                    } into h
                                    select new
                                    {   
                                        transaction_no = h.Max(m => m.transaction_no)
                                    }
                                  ) on b.transaction_no equals max.transaction_no 
                              select new
                              { 
                                  b.main_job_no,
                                  b.sub_job_no,
                                  b.process_status,
                                  b.process_code,
                                  b.machine_no,
                                  b.start_date,
                                  b.start_time,
                                  b.finished_date,
                                  b.finished_time,
                                  b.finished_user_name
                              })

                      on new { tblSubJobList.main_job_no, tblSubJobList.sub_job_no } equals new { td_progress.main_job_no, td_progress.sub_job_no } into group1
                      from td_progress in group1.DefaultIfEmpty()

                      join process_name in dbTooldie.tr_process on td_progress.process_code.Trim() equals process_name.proc_code.Trim() into group2
                      from process_name in group2.DefaultIfEmpty()

                      join subjob in dbTooldie.tr_sub_job on new { tblSubJobList.main_job_no, tblSubJobList.sub_job_no } equals
                                                                  new { subjob.main_job_no, subjob.sub_job_no } into group3
                      from subjob in group3.DefaultIfEmpty()

                      join rack in dbTooldie.tm_rack_part on new { td_progress.main_job_no, td_progress.sub_job_no } equals new { rack.main_job_no, rack.sub_job_no } into group4
                      
                      from rack in group4.DefaultIfEmpty()

                      select new
                      {
                          tblSubJobList.main_job_no,        // Main Job No.
                          tblSubJobList.sub_job_no,         // Sub Job No.
                          subjob.description,               // Description
                          subjob.qty,                       // Q'ty
                          subjob.wo_evaluation,             // Source
                          td_progress.process_status,       // Status
                          process_name.proc_name,           // Process
                          rack.rack_no,                     // Rack No.
                          td_progress.machine_no,           // Machine
                          td_progress.start_date,           // Start Date
                          td_progress.start_time,           // Start Time
                          td_progress.finished_date,        // Finish Date
                          td_progress.finished_time,        // Finish Time
                          td_progress.finished_user_name    // O/P Name

                      }).Where(x => x.main_job_no.Trim() == job_no.Trim());

            var pageIndex = Convert.ToInt32(page) - 1;
            var pageSize = rows;
            var totalRecords = db == null ? 0 : db.Count();
            //var totalRecords = db.Count();
            var totalPages = (int)Math.Ceiling(totalRecords / (float)pageSize);

            // This is possible because I'm using the LINQ Dynamic Query Library
            var store = db.AsQueryable()
                    .OrderBy(sidx + " " + sord)
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize);

            var jsonData = new
            {
                total = totalPages,
                page = page,
                records = totalRecords,
                rows = store.ToList().Select(q => new
                {
                    i = q.main_job_no,
                    cell = new object[] { 
                        q.sub_job_no, 
                        q.description,
                        q.qty,
                        q.wo_evaluation,
                        q.process_status,
                        q.proc_name,                        
                        q.rack_no,
                        q.machine_no,
                        q.start_date,
                        formattime(q.start_time),
                        //q.start_time,
                        q.finished_date,
                        formattime(q.finished_time),
                        q.finished_user_name
                    }

                }).ToArray()
            };

            return Json(jsonData);
        }

        public string formattime(string time)
        {
            string f = "";

            if (!string.IsNullOrEmpty(time.Trim()) && (time.Trim().Length >= 6)) 
            {              
                f = string.Format("{0:00:00:00}", Convert.ToInt32(time));
            }
            
            return f;
        }

        public string NewTimeFormat(string time)
        {
            string stime = time.Trim();
            if (!string.IsNullOrEmpty(stime) && stime.Length == 6)
            {
                return stime.Substring(0, stime.Length - 4) + ":" + stime.Substring(stime.Length - 4, 2);
            }
            else
            {
                return "";
            }
        }

        public string NewDateFormat(string date)
        {
            string sdate = date.Trim();
            if (!string.IsNullOrEmpty(sdate) && sdate.Length == 8)
            {
                return sdate.Substring(0, 4) + "-" + sdate.Substring(4, 2) + "-" + sdate.Substring(6, 2);
            }
            else
            {
                return "";
            }
        }

        private string ToPGDateFormat(string date)
        {
            string sdate = date.Trim();
            if (!string.IsNullOrEmpty(sdate) && sdate.Length == 10)
            {
                return sdate.Substring(6) + sdate.Substring(3, 2) + sdate.Substring(0, 2);
            }
            else
            {
                return "";
            }
        }

        public ActionResult Rack_Mold()
        {
            if (chkSession()) return RedirectToAction("Login", "Account");

            var data = (from i in dbTooldie.tr_process select i);

            ViewBag.process = data.Select(s => s.proc_name.Trim()).Distinct();

            return View();
        }

        public ActionResult GetMax_RackMold()
        {
            string ret = null;
        

            var max = (from c in dbTooldie.tm_rack_mold
                       select c).OrderByDescending(x => x.rack_no).FirstOrDefault();

            if (max != null)
            {
                string maxRack = max.rack_no;
                string runNo = maxRack.Substring(5, 7);            
                //ret = string.Format("{0:RMOLD0000000}", Convert.ToInt32(runNo) + 1);
                ret = string.Format("{0:0000000}", Convert.ToInt32(runNo) + 1);
            }
            else 
            {
                //ret = string.Format("{0:RMOLD0000000}", 0000001);
                ret = string.Format("{0:0000000}", 0000001);
            }
            
            return Json(ret, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Insert_RackMD (string user, string rack)
        {
            string rackmold = "RMOLD";

            var chkRack = (from i in dbTooldie.tm_rack_mold
                           where i.rack_no.Trim() == rackmold + rack.Trim()
                           select i).FirstOrDefault();

            if (chkRack == null)
            {
                tm_rack_mold tmRackMold = new tm_rack_mold();

                tmRackMold.rack_no = rackmold + rack.Trim();
                tmRackMold.status_use = "0";
                tmRackMold.location_cd = "PTN";
                tmRackMold.update_user = user;
                tmRackMold.update_date = DateTime.Now.ToString("yyyyMMdd");
                tmRackMold.update_time = DateTime.Now.ToString("HHmmss");

                dbTooldie.tm_rack_mold.Add(tmRackMold);
            }

            var ret = dbTooldie.SaveChanges();

            return Json(ret);
        }

        public ActionResult Clear_RackMD (string user, string rack)
        {
            var chkRack = (from i in dbTooldie.tm_rack_mold
                           where i.rack_no.Trim() == rack.Trim()
                           select i).FirstOrDefault();
            
            tm_rack_mold tmRackMold = new tm_rack_mold();
           
            if (chkRack.status_use == "1") // Status: In use
            {
                chkRack.status_use = "0";
                chkRack.proc_code = "";
                chkRack.proc_name = "";
                chkRack.main_job_no = "";
                chkRack.sub_job_no = "";
                chkRack.item = "";
                chkRack.update_date = "";
                chkRack.update_time = "";
                chkRack.update_user = "";
            }

            var ret = dbTooldie.SaveChanges();
            return Json(ret);
        }

        public ActionResult Delete_RackMD (string rack)
        {
            var delRack = dbTooldie.tm_rack_mold.Find(rack);
            dbTooldie.tm_rack_mold.Remove(delRack);
            var ret = dbTooldie.SaveChanges();

            return Json(ret);

        }

        public ActionResult Rack_Part()
        {
            if (chkSession()) return RedirectToAction("Login", "Account");

            var data = (from i in dbTooldie.tr_process select i);

            ViewBag.process = data.Select(s => s.proc_name.Trim()).Distinct();

            return View();
        }

        public ActionResult Insert_RackPart(string user, string rack, string process)
        {
            var chkRack = (from i in dbTooldie.tm_rack_process                          
                           where i.rack_no.Trim() == rack.Trim()
                           select i).FirstOrDefault();
 
            if (chkRack == null)
            {
                // Add
                tm_rack_process tmRackProcess = new tm_rack_process();

                tmRackProcess.rack_no = rack.Trim();
                tmRackProcess.process_code = "PC" + rack.Substring(2, 4);
                tmRackProcess.entry_date = DateTime.Now.ToString("yyyyMMdd");
                tmRackProcess.entry_time = DateTime.Now.ToString("HHmmss");
                tmRackProcess.entry_user = user;

                dbTooldie.tm_rack_process.Add(tmRackProcess);
            }

            var ret = dbTooldie.SaveChanges();

            return Json(ret);
        }

        public ActionResult Delete_RackPart(string rack, string mainjob, string subjob)
        {          
            var delRack = dbTooldie.tm_rack_part.Find(rack, mainjob, subjob);
            dbTooldie.tm_rack_part.Remove(delRack);
            var ret = dbTooldie.SaveChanges();

            return Json(ret);
        }

        public ActionResult Delete_RackPart_Master(string rack)
        {
            var query = (from i in dbTooldie.tm_rack_part
                        where i.rack_no.Trim() == rack.Trim()
                        select i).FirstOrDefault();

            if (query == null) // Rack Empty 
            {
                var delRack = dbTooldie.tm_rack_process.Find(rack);
                dbTooldie.tm_rack_process.Remove(delRack);
            }

            var ret = dbTooldie.SaveChanges();
            return Json(ret);
        }

        public ActionResult CreateGridRackPart_Master(string sidx, string sord, int page, int rows, string process)
        {
            // Select r.rack_no, m.proc_name from tm_rack_process as r join tr_process as m on r.process_code = m.proc_code 

            var db = (from a in dbTooldie.tm_rack_process
                      join b in dbTooldie.tr_process on a.process_code.Trim() equals b.proc_code.Trim() into g
                      from b in g.DefaultIfEmpty()
                      select new
                      {
                          a.rack_no,
                          b.proc_name
                      });

            if (!string.IsNullOrEmpty(process))
            {
                db = db.Where(x => x.proc_name.Trim() == process);
            }

            var pageIndex = Convert.ToInt32(page) - 1;
            var pageSize = rows;
            var totalRecords = db == null ? 0 : db.Count();
            //var totalRecords = db.Count();
            var totalPages = (int)Math.Ceiling(totalRecords / (float)pageSize);

            // This is possible because I'm using the LINQ Dynamic Query Library
            var store = db.AsQueryable()
                    .OrderBy(sidx + " " + sord)
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize);

            var jsonData = new
            {
                total = totalPages,
                page = page,
                records = totalRecords,
                rows = store.ToList().Select(q => new
                {                  
                    cell = new object[] { 
                       q.rack_no,
                       q.proc_name,
                       "<a href = '#' class='lnkPrintQR'  data-rack='" + q.rack_no + "'><img src='../Images/barcode-2d-icon.png' alt='Print' /></a>",
                       "<a href = '#' style='color:red;' class='lnkDelRackMaster'  data-rack='" + q.rack_no + "'><img src='../Images/Trash-can-icon2.png' alt='Delete' /></a>"
                    }

                }).ToArray()
            };

            return Json(jsonData);
                
        }

        public ActionResult CreateGridRackPart(string sidx, string sord, int page, int rows, string rack, string process)
        {
            var db = (from a in dbTooldie.tm_rack_part
                      select new
                      {
                          a.rack_no,
                          a.status_use,
                          a.main_job_no,
                          a.sub_job_no,
                          a.item,
                          a.proc_name,
                          a.update_date,
                          a.update_user
                      });

            if (!string.IsNullOrEmpty(rack) && !string.IsNullOrEmpty(process))
            {
                db = db.Where(x => x.rack_no.StartsWith(rack.Trim()) && x.proc_name.Trim() == process.Trim());
            }
            else if (!string.IsNullOrEmpty(rack) && string.IsNullOrEmpty(process))
            {
                db = db.Where(x => x.rack_no.StartsWith(rack));
            }
            else if (string.IsNullOrEmpty(rack) && !string.IsNullOrEmpty(process))
            {
                db = db.Where(x => x.proc_name.Trim() == process);
            }

            var pageIndex = Convert.ToInt32(page) - 1;
            var pageSize = rows;
            var totalRecords = db == null ? 0 : db.Count();
            //var totalRecords = db.Count();
            var totalPages = (int)Math.Ceiling(totalRecords / (float)pageSize);

            // This is possible because I'm using the LINQ Dynamic Query Library
            var store = db.AsQueryable()
                    .OrderBy(sidx + " " + sord)
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize);

            var jsonData = new
            {
                total = totalPages,
                page = page,
                records = totalRecords,
                rows = store.ToList().Select(q => new
                {
                    i = q.main_job_no,
                    j = q.sub_job_no,
                    cell = new object[] { 

                        q.rack_no, 
                        q.main_job_no,
                        q.sub_job_no,
                        q.item,
                        q.proc_name,                        
                        q.update_date,
                        q.update_user,
                        "<a href ='#' style='color:red;' class='lnkDel' data-rack='" + q.rack_no.Trim() + "' data-mainjob= '"+ q.main_job_no.Trim() +"' data-subjob = '"+ q.sub_job_no.Trim() +"' ><img src='../Images/Trash-can-icon2.png' alt='Delete' /></a>"    
                    }

                }).ToArray()
            };

            return Json(jsonData);
        }

        public ActionResult CreateGridRackMold(string sidx, string sord, int page, int rows, string rack)
        {
            var db = (from a in dbTooldie.tm_rack_mold
                      select new
                      {
                          a.rack_no,
                          a.status_use,
                          a.main_job_no,
                          a.sub_job_no,
                          a.item,                        
                          a.update_date,
                          a.update_user
                      });

            if (!string.IsNullOrEmpty(rack))
            {
                db = db.Where(x => x.rack_no.StartsWith(rack.Trim()));
            }
          
            var pageIndex = Convert.ToInt32(page) - 1;
            var pageSize = rows;
            var totalRecords = db == null ? 0 : db.Count();
            //var totalRecords = db.Count();
            var totalPages = (int)Math.Ceiling(totalRecords / (float)pageSize);

            // This is possible because I'm using the LINQ Dynamic Query Library
            var store = db.AsQueryable()
                    .OrderBy(sidx + " " + sord)
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize);

            var jsonData = new
            {
                total = totalPages,
                page = page,
                records = totalRecords,
                rows = store.ToList().Select(q => new
                {
                    i = q.main_job_no,
                    j = q.sub_job_no,
                    cell = new object[] { 
                       
                        q.status_use == "0" ? "<img src='../Images/icons/Button-Blank-Green-icon.png' alt='Empty' />" : "<img src='../Images/icons/Button-Blank-Red-icon.png' alt='Empty' />",
                        //q.status_use == "0" ? "Clear" : "Delete", 
                        q.rack_no,                         
                        q.main_job_no,
                        q.sub_job_no,
                        q.item,                                          
                        q.update_date,
                        q.update_user,                     
                       "<a href ='#' style='color:blue;' class='lnkEdit' data-rack='" + q.rack_no.Trim() + "' >Clear</a>&nbsp;<a href ='#' style='color:red;' class='lnkDel' data-rack='" + q.rack_no.Trim() + "' >Del</a>",  
                       "<a href ='#' class='lnkPrintQR' data-rack='" + q.rack_no.Trim() + "' ><img src='../Images/barcode-2d-icon.png' alt='Print' /></a> "
                    }

                }).ToArray()
            };

            return Json(jsonData);
        }

        public ActionResult BarcodeImage (string racknum)
        {
            QrEncoder qrEncoder = new QrEncoder(ErrorCorrectionLevel.H);
            QrCode qrCode = new QrCode();
            qrEncoder.TryEncode(racknum, out qrCode);
            GraphicsRenderer renderer = new GraphicsRenderer(new FixedModuleSize(4, QuietZoneModules.Four), Brushes.Black, Brushes.White);

            Stream memoryStream = new MemoryStream();
            renderer.WriteToStream(qrCode.Matrix, ImageFormat.Png, memoryStream);

            memoryStream.Position = 0;

            var resultStream = new FileStreamResult(memoryStream, "image/png");
            resultStream.FileDownloadName = String.Format("{0}.png", racknum);

            return resultStream;
        }

        public ActionResult GetSubJob(string mainjob)
        {
            var query = from a in dbTooldie.tr_sub_job
                        where a.main_job_no.Trim() == mainjob.Trim()
                        select new
                        {
                            subjob = a.sub_job_no.Trim()
                        };

            if (query != null)
                return Json(query, JsonRequestBehavior.AllowGet);
            else
                return Json(0, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetMainJobDetail(string mainjob)
        {
            var query = (from a in dbTooldie.tr_main_job
                         where a.main_job_no.Trim() == mainjob.Trim()
                         select new
                         {
                             a.ref_old_td,
                             a.item,
                             a.product_type_name,
                             a.description,
                             a.qty,
                             a.unit,
                             a.customer_name,
                             promise_date = a.promise_date.Substring(0, 4) + "-" + a.promise_date.Substring(4, 2) + "-" + a.promise_date.Substring(6, 2),
                             due_date = a.due_date.Substring(0, 4) + "-" + a.due_date.Substring(4, 2) + "-" + a.due_date.Substring(6, 2),
                             a.wo_evaluation,
                             a.plant_div_name
                         }).FirstOrDefault();
            if (query != null)
                return Json(query, JsonRequestBehavior.AllowGet);
            else
                return Json(0, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetSubJobDetail(string mainjob, string subjob)
        {
            var query = (from a in dbTooldie.tr_sub_job
                             .Where(w => w.main_job_no.Trim() == mainjob.Trim() && w.sub_job_no.Trim() == subjob.Trim())
                         join b in dbTooldie.tm_rack_part 
                         on new{ a.main_job_no, a.sub_job_no } equals new{ b.main_job_no, b.sub_job_no } into g
                         from b in g.DefaultIfEmpty()
                         select new
                         {
                             a.item_part_name,
                             a.qty,
                             a.unit,
                             a.description,
                             a.wo_evaluation,
                             b.proc_name,
                             b.rack_no,
                             b.update_user
                         }).FirstOrDefault();

            if (query != null)
                return Json(query, JsonRequestBehavior.AllowGet);
            else
                return Json(0, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetProgressive(string mainjob, string subjob)
        {
            var total = (from b in dbTooldie.tr_job_progress
                         where b.main_job_no.Trim() == mainjob.Trim() && b.sub_job_no.Trim() == subjob.Trim()
                         select b).Count();
            var comp = (from a in dbTooldie.td_job_progress
                        where a.main_job_no.Trim() == mainjob.Trim() && a.sub_job_no.Trim() == subjob.Trim()
                        && a.process_status.Trim() == "Y"
                        group a by a.marking_step into g
                        select g.Key).Count();
            var percent = total == 0 ? 0.0 : (comp / (double)total) * 100;

            return Json(percent.ToString("F", CultureInfo.InvariantCulture) + "%", JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult ProgressiveList(string mainjob, string subjob, int jtStartIndex = 0, int jtPageSize = 0, string jtSorting = null)
        {
            try
            {
                var query = (
                    from a in dbTooldie.tr_job_progress
                        .Where(w => w.main_job_no.Trim() == mainjob.Trim() && w.sub_job_no.Trim() == subjob.Trim())
                    join b in dbTooldie.td_job_progress.OrderBy(o => o.transaction_no)
                    on new { a.main_job_no, a.sub_job_no, x = a.marking_step } equals new { b.main_job_no, b.sub_job_no, x = b.marking_step.Value } into g
                    from b in g.DefaultIfEmpty()
                    join c in dbTooldie.tr_process on a.process_code.Trim() equals c.proc_code.Trim() into p
                    from c in p.DefaultIfEmpty()
                    join d in dbTooldie.tr_machine on new { m = b.machine_no.Trim(), p = b.process_code } equals new { m = d.machine_code.Trim(), p = d.proc_code } into q
                    from d in q.DefaultIfEmpty()
                    join e in dbTooldie.td_comment on new { a.main_job_no, a.sub_job_no, a.marking_step } equals new { e.main_job_no, e.sub_job_no, e.marking_step } into cm
                    from e in cm.DefaultIfEmpty()
                    select new
                    {
                        a.main_job_no,
                        a.sub_job_no,
                        a.marking_step,
                        c.proc_name,
                        std_time = !string.IsNullOrEmpty(b.type_est) ? (b.type_est.Trim() == "S" ? a.est_setup_time : a.est_machine_time) : 0,
                        machine_name = !string.IsNullOrEmpty(d.machine_name) ? d.machine_name : "",
                        start_date = !string.IsNullOrEmpty(b.start_date) ? b.start_date.Substring(0, 4) + "-" + b.start_date.Substring(4, 2) + "-" + b.start_date.Substring(6, 2) : "",
                        start_time = !string.IsNullOrEmpty(b.start_time) ? b.start_time.Substring(0, b.start_time.Length - 4) + ":" + b.start_time.Substring(b.start_time.Length - 4, 2) : "",
                        finished_date = !string.IsNullOrEmpty(b.finished_date) ? b.finished_date.Substring(0, 4) + "-" + b.finished_date.Substring(4, 2) + "-" + b.finished_date.Substring(6, 2) : "",
                        finished_time = !string.IsNullOrEmpty(b.finished_time) ? b.finished_time.Substring(0, b.finished_time.Length - 4) + ":" + b.finished_time.Substring(b.finished_time.Length - 4, 2) : "",
                        finished_user_name = !string.IsNullOrEmpty(b.finished_user_name) ? b.finished_user_name : (!string.IsNullOrEmpty(b.start_user_name) ? b.start_user_name : ""),
                        finished_user_id = !string.IsNullOrEmpty(b.finished_user_id) ? b.finished_user_id : (!string.IsNullOrEmpty(b.start_user_id) ? b.start_user_id : ""),
                        process_status = !string.IsNullOrEmpty(b.process_status) ? b.process_status : "N",
                        type_est = !string.IsNullOrEmpty(b.type_est) ? (b.type_est.Trim() == "S" ? "SETUP" : "MACHINE") : "",
                        comment = !string.IsNullOrEmpty(e.comment_process) ? e.comment_process : "",
                        //b.process_code,
                        //b.machine_no,
                        //d.proc_code,
                        //d.machine_code
                    });

                //Get data from databaset
                int TotalRecord = query.Count();

                // Paging
                var output = query.ToList()
                    .Select(s => new
                    {
                        s.main_job_no,
                        s.sub_job_no,
                        s.marking_step,
                        s.proc_name,
                        s.process_status,
                        s.start_date,
                        s.start_time,
                        s.finished_date,
                        s.finished_time,
                        spend_time = CalSpendTimetoMinute(s.start_date, s.start_time, s.finished_date, s.finished_time),
                        s.std_time,
                        std_diff = CalSpendTimetoMinute(s.start_date, s.start_time, s.finished_date, s.finished_time, s.std_time.ToString()),
                        s.machine_name,
                        s.finished_user_name,
                        s.finished_user_id,
                        s.type_est,
                        s.comment,
                        //s.process_code,
                        //s.machine_no,
                        //s.proc_code,
                        //s.machine_code
                    }).AsQueryable().OrderBy(jtSorting).Skip(jtStartIndex).Take(jtPageSize);

                //Return result to jTable
                return Json(new { Result = "OK", Records = output, TotalRecordCount = TotalRecord });
            }
            catch (Exception ex)
            {
                return Json(new { Result = "ERROR", Message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult ProcessTimeList(string mainjob, string subjob, string start, string end, string process, int jtStartIndex = 0, int jtPageSize = 0, string jtSorting = null)
        {
            try
            {
                var progress = dbTooldie.td_job_progress.AsQueryable();

                if (!string.IsNullOrEmpty(mainjob))
                {
                    progress = progress.Where(w => w.main_job_no.Trim() == mainjob.Trim());
                }

                if (!string.IsNullOrEmpty(subjob))
                {
                    progress = progress.Where(w => w.sub_job_no.Trim() == subjob.Trim());
                }

                if (!string.IsNullOrEmpty(start) && !string.IsNullOrEmpty(end))
                {
                    var from = ToPGDateFormat(start);
                    var to = ToPGDateFormat(end);
                    progress = progress.Where(w => w.start_date.CompareTo(from) >= 0 && w.start_date.CompareTo(to) <= 0);
                }

                if (!string.IsNullOrEmpty(process))
                {
                    progress = progress.Where(w => w.process_code.Trim() == process.Trim());
                }

                var join1 = from p in progress.Where(w => w.type_est == "M")
                            join t in dbTooldie.ts_process_result on p.transaction_no equals t.transaction_no
                            group new { p, t } by new { p.main_job_no, p.sub_job_no, p.marking_step, p.type_est, p.qty, p.machine_no, p.process_code } into x
                            select new
                            {
                                x.Key.main_job_no,
                                x.Key.sub_job_no,
                                x.Key.marking_step,
                                x.Key.qty,
                                spend_time = x.Sum(s => s.t.spent_time),
                                x.Key.machine_no,
                                x.Key.process_code,
                                process_status = x.Max(s => s.p.process_status)
                            };

                var join2 = from p in progress.Where(w => w.type_est == "S")
                            join t in dbTooldie.ts_process_result on p.transaction_no equals t.transaction_no
                            group new { p, t } by new { p.main_job_no, p.sub_job_no, p.marking_step, p.type_est, p.qty, p.machine_no, p.process_code } into x
                            select new
                            {
                                x.Key.main_job_no,
                                x.Key.sub_job_no,
                                x.Key.marking_step,
                                x.Key.qty,
                                spend_time = x.Sum(s => s.t.spent_time),
                                x.Key.machine_no,
                                x.Key.process_code,
                                process_status = x.Max(s => s.p.process_status)
                            };

                var query = from a in join1
                            join b in join2
                            on new { a.main_job_no, a.sub_job_no, a.marking_step } equals new { b.main_job_no, b.sub_job_no, b.marking_step }
                            into temp
                            from b in temp.DefaultIfEmpty()
                            join c in dbTooldie.tr_main_job on a.main_job_no equals c.main_job_no into g1
                            from c in g1.DefaultIfEmpty()
                            join d in dbTooldie.tr_job_progress on new { a.main_job_no, a.sub_job_no, x = a.marking_step.Value } equals new { d.main_job_no, d.sub_job_no, x = d.marking_step } into g2
                            from d in g2.DefaultIfEmpty()
                            join e in dbTooldie.tr_process on a.process_code equals e.proc_code into g3
                            from e in g3.DefaultIfEmpty()
                            select new
                            {
                                a.main_job_no,
                                a.sub_job_no,
                                a.qty,
                                a.machine_no,
                                e.proc_name,
                                a.process_status,
                                c.item,
                                s_plan = d.est_setup_time,
                                s_act = b.spend_time != null ? b.spend_time : default(int),
                                m_plan = d.est_machine_time,
                                m_act = a.spend_time != null ? a.spend_time : default(int)
                            };

                //Get data from databaset
                int TotalRecord = query.Count();

                // Paging
                var output = query.OrderBy(jtSorting).Skip(jtStartIndex).Take(jtPageSize).ToList()
                    .Select(s => new
                    {
                        s.main_job_no,
                        s.sub_job_no,
                        //s.marking_step,
                        s.qty,
                        s.item,
                        s.proc_name,
                        s.machine_no,
                        s.process_status,
                        s.s_plan,
                        s.s_act,
                        s.m_plan,
                        s.m_act
                    }).AsQueryable();

                //Return result to jTable
                return Json(new { Result = "OK", Records = output, TotalRecordCount = TotalRecord });
            }
            catch (Exception ex)
            {
                return Json(new { Result = "ERROR", Message = ex.Message });
            }
        }

        [HttpPost]
        public void ExportProcessTime()
        {
            string mainjob = Request.Form["txtMain"];
            string subjob = Request.Form["txtSub"];
            string start = Request.Form["date_start"];
            string end = Request.Form["date_end"];
            string process = Request.Form["DropDown_Proc"];

            var progress = dbTooldie.td_job_progress.AsQueryable();

            if (!string.IsNullOrEmpty(mainjob))
            {
                progress = progress.Where(w => w.main_job_no.Trim() == mainjob.Trim());
            }

            if (!string.IsNullOrEmpty(subjob))
            {
                progress = progress.Where(w => w.sub_job_no.Trim() == subjob.Trim());
            }

            if (!string.IsNullOrEmpty(start) && !string.IsNullOrEmpty(end))
            {
                var from = ToPGDateFormat(start);
                var to = ToPGDateFormat(end);
                progress = progress.Where(w => w.start_date.CompareTo(from) >= 0 && w.start_date.CompareTo(to) <= 0);
            }

            if (!string.IsNullOrEmpty(process))
            {
                progress = progress.Where(w => w.process_code.Trim() == process.Trim());
            }

            var join1 = from p in progress.Where(w => w.type_est == "M")
                        join t in dbTooldie.ts_process_result on p.transaction_no equals t.transaction_no
                        group new { p, t } by new { p.main_job_no, p.sub_job_no, p.marking_step, p.type_est, p.qty, p.machine_no, p.process_code } into x
                        select new
                        {
                            x.Key.main_job_no,
                            x.Key.sub_job_no,
                            x.Key.marking_step,
                            x.Key.qty,
                            spend_time = x.Sum(s => s.t.spent_time),
                            x.Key.machine_no,
                            x.Key.process_code,
                            process_status = x.Max(s => s.p.process_status)
                        };

            var join2 = from p in progress.Where(w => w.type_est == "S")
                        join t in dbTooldie.ts_process_result on p.transaction_no equals t.transaction_no
                        group new { p, t } by new { p.main_job_no, p.sub_job_no, p.marking_step, p.type_est, p.qty, p.machine_no, p.process_code } into x
                        select new
                        {
                            x.Key.main_job_no,
                            x.Key.sub_job_no,
                            x.Key.marking_step,
                            x.Key.qty,
                            spend_time = x.Sum(s => s.t.spent_time),
                            x.Key.machine_no,
                            x.Key.process_code,
                            process_status = x.Max(s => s.p.process_status)
                        };

            var query = from a in join1
                        join b in join2
                        on new { a.main_job_no, a.sub_job_no, a.marking_step } equals new { b.main_job_no, b.sub_job_no, b.marking_step }
                        into temp
                        from b in temp.DefaultIfEmpty()
                        join c in dbTooldie.tr_main_job on a.main_job_no equals c.main_job_no into g1
                        from c in g1.DefaultIfEmpty()
                        join d in dbTooldie.tr_job_progress on new { a.main_job_no, a.sub_job_no, x = a.marking_step.Value } equals new { d.main_job_no, d.sub_job_no, x = d.marking_step } into g2
                        from d in g2.DefaultIfEmpty()
                        join e in dbTooldie.tr_process on a.process_code equals e.proc_code into g3
                        from e in g3.DefaultIfEmpty()
                        join f in dbTooldie.tr_machine on new { mc = a.machine_no, pc = a.process_code } equals new { mc = f.machine_code, pc = f.proc_code } into g4
                        from f in g4.DefaultIfEmpty()
                        orderby a.main_job_no, a.sub_job_no, a.marking_step
                        select new
                        {
                            a.main_job_no,
                            a.sub_job_no,
                            a.qty,
                            a.machine_no,
                            f.machine_name,
                            e.proc_name,
                            a.process_status,
                            c.item,
                            c.product_type_name,
                            s_plan = d.est_setup_time,
                            s_act = b.spend_time != null ? b.spend_time : default(int),
                            m_plan = d.est_machine_time,
                            m_act = a.spend_time != null ? a.spend_time : default(int)
                        };

            var output = query.ToList()
                    .Select(s => new
                    {
                        MAINJOB = s.main_job_no,
                        YTYPE = s.product_type_name,
                        PARTNO = s.item,
                        SUBJOB = s.sub_job_no,
                        QTY = s.qty,
                        PROCESS = s.proc_name,
                        MACHINE = s.machine_no,
                        MCName = s.machine_name,
                        STATUS = s.process_status,
                        S_PLN = s.s_plan,
                        S_ACT = s.s_act,
                        S_DIF = s.s_act - s.s_plan,
                        M_PLN = s.m_plan,
                        M_ACT = s.m_act,
                        M_DIF = s.m_act - s.m_plan,
                        T_PLN = s.s_plan + s.m_plan,
                        T_ACT = s.s_act + s.m_act,
                        T_DIF = s.s_act + s.m_act - s.s_plan - s.m_plan
                    }).ToList();

            TNCUtility util = new TNCUtility();
            util.CreateExcel(output, "ProcessTime_" + DateTime.Now.ToString("yyyyMMdd"));
        }

        public string CalSpendTime(string sd, string st, string fd, string ft)
        {
            if (!string.IsNullOrEmpty(fd) && !string.IsNullOrEmpty(ft) && !string.IsNullOrEmpty(sd) && !string.IsNullOrEmpty(st))
            {
                DateTime stv, ftv;
                TimeSpan span;
                string result = "";
                var start_dt = sd + " " + st;
                var finish_dt = fd + " " + ft;
                if (DateTime.TryParse(start_dt, out stv) && DateTime.TryParse(finish_dt, out ftv))
                {
                    span = ftv.Subtract(stv);
                    result = span.ToString(@"hh\:mm");
                }
                return result;
            }
            else
            {
                return "";
            }
        }

        public int CalSpendTimetoMinute(string sd, string st, string fd, string ft, string std_time=null)
        {
            if (!string.IsNullOrEmpty(fd) && !string.IsNullOrEmpty(ft) && !string.IsNullOrEmpty(sd) && !string.IsNullOrEmpty(st))
            {
                DateTime stv, ftv;
                TimeSpan span;
                int result = 0;
                var start_dt = sd + " " + st;
                var finish_dt = fd + " " + ft;
                if (DateTime.TryParse(start_dt, out stv) && DateTime.TryParse(finish_dt, out ftv))
                {
                    span = ftv.Subtract(stv);
                    
                    if (std_time != null)
                    {
                        result = int.Parse(std_time) - (int)span.TotalMinutes;
                    }
                    else
                    {
                        result = (int)span.TotalMinutes;
                    }
                    //result = span.ToString(@"hh\:mm");
                }
                return result;
            }
            else
            {
                return 0;
            }
        }

        public int CalTimetoMinute(string hhmm)
        {
            if (!string.IsNullOrEmpty(hhmm) && hhmm.Length == 5)
            {
                int hh = int.Parse(hhmm.Substring(0, 2));
                int mm = int.Parse(hhmm.Substring(3, 2));
                int time = (hh * 60) + mm;
                return time;
            }
            else
            {
                return 0;
            }
        }

        [HttpPost]
        public void ExportProgress()
        {
            TNCUtility util = new TNCUtility();
            string mainjob = Request.Form["hdmain"];
            string subjob = Request.Form["hdsub"];

            var query = (
                    from a in dbTooldie.tr_job_progress
                        .Where(w => w.main_job_no.Trim() == mainjob.Trim() && w.sub_job_no.Trim() == subjob.Trim())
                    join b in dbTooldie.td_job_progress.OrderBy(o => o.transaction_no)
                    on new { a.main_job_no, a.sub_job_no, x = a.marking_step } equals new { b.main_job_no, b.sub_job_no, x = b.marking_step.Value } into g
                    from b in g.DefaultIfEmpty()
                    join c in dbTooldie.tr_process on a.process_code.Trim() equals c.proc_code.Trim() into p
                    from c in p.DefaultIfEmpty()
                    join d in dbTooldie.tr_machine on new { m = b.machine_no.Trim(), p = b.process_code } equals new { m = d.machine_code.Trim(), p = d.proc_code } into q
                    from d in q.DefaultIfEmpty()
                    join e in dbTooldie.td_comment on new { a.main_job_no, a.sub_job_no, a.marking_step } equals new { e.main_job_no, e.sub_job_no, e.marking_step } into x
                    from e in x.DefaultIfEmpty()
                    select new
                    {
                        a.marking_step,
                        c.proc_name,
                        process_status = !string.IsNullOrEmpty(b.process_status) ? b.process_status : "N",
                        start_date = !string.IsNullOrEmpty(b.start_date) ? b.start_date.Substring(0, 4) + "-" + b.start_date.Substring(4, 2) + "-" + b.start_date.Substring(6, 2) : "",
                        start_time = !string.IsNullOrEmpty(b.start_time) ? b.start_time.Substring(0, b.start_time.Length - 4) + ":" + b.start_time.Substring(b.start_time.Length - 4, 2) : "",// + ":" + b.start_time.Substring(b.start_time.Length - 2, 2) : "",
                        finished_date = !string.IsNullOrEmpty(b.finished_date) ? b.finished_date.Substring(0, 4) + "-" + b.finished_date.Substring(4, 2) + "-" + b.finished_date.Substring(6, 2) : "",
                        finished_time = !string.IsNullOrEmpty(b.finished_time) ? b.finished_time.Substring(0, b.finished_time.Length - 4) + ":" + b.finished_time.Substring(b.finished_time.Length - 4, 2) : "",// + ":" + b.finished_time.Substring(b.finished_time.Length - 2, 2) : "",
                        machine_name = !string.IsNullOrEmpty(d.machine_name) ? d.machine_name.Trim() : "",
                        finished_user_name = !string.IsNullOrEmpty(b.finished_user_name) ? b.finished_user_name.Trim() : "",
                        type_est = !string.IsNullOrEmpty(b.type_est) ? (b.type_est.Trim() == "S" ? "SETUP" : "MACHINE") : "",
                        e.comment_process
                    }).ToList();

            var output = query
                    .Select(s => new
                    {
                        No = s.marking_step,
                        ProcessName = s.proc_name.Trim(),
                        TypeEst = s.type_est,
                        Status = s.process_status,
                        Start_Date = s.start_date,
                        Start_Time = s.start_time,
                        Finish_Date = s.finished_date,
                        Finish_Time = s.finished_time,
                        Spend_Time = CalSpendTimetoMinute(s.start_date, s.start_time, s.finished_date, s.finished_time),
                        Machine = s.machine_name,
                        Operator = s.finished_user_name,
                        Comment = s.comment_process
                    }).OrderBy(o => o.No).ToList();

            util.CreateExcel(output, "Progressive_" + DateTime.Now.ToString("yyyy-MM-dd"));
        }

        public ActionResult UserSkill()
        {
            if (chkSession()) return RedirectToAction("Login", "Account");
            var all_user = from a in dbTooldie.tm_user_id
                           orderby a.first_name, a.last_name
                           select a;
            ViewBag.User = all_user;
            var all_process = from a in dbTooldie.tr_process
                              where a.skill_type == "PC" && a.del_flag == 0
                              orderby a.proc_name
                              select a;
            var knowledge = from a in dbTooldie.tr_process
                            where a.skill_type == "KL" && a.del_flag == 0
                            orderby a.proc_name
                            select a;
            ViewBag.Process = all_process;
            ViewBag.Knowledge = knowledge;
            return View();
        }

        [HttpPost]
        public ActionResult UpdateUserSkill(string user, string rank, string name, string code)
        {
            try
            {
                var js = new JavaScriptSerializer();
                var obj = js.DeserializeObject(code);

                var query = from a in dbTooldie.td_user_skill
                            where a.user_id == user
                            select a;
                foreach (var skill in query)
                {
                    dbTooldie.td_user_skill.Remove(skill);
                }

                foreach (var item in (IEnumerable)obj)
                {
                    var arrVal = ((IEnumerable)item).Cast<object>()
                        .Select(x => x == null ? "" : x.ToString()).ToArray();

                    var uskill = new td_user_skill();
                    uskill.user_id = user;
                    uskill.proc_code = arrVal[0];
                    uskill.evaluation = bool.Parse(arrVal[1]);
                    uskill.skill_type = arrVal[2];
                    uskill.update_date = DateTime.Now.ToString("yyyyMMdd");
                    dbTooldie.td_user_skill.Add(uskill);
                }

                var updateRank = (from a in dbTooldie.tm_user_id
                                  where a.user_id == user
                                  select a).FirstOrDefault();

                if (updateRank != null)
                {
                    updateRank.rank = rank;
                }

                dbTooldie.SaveChanges();

                return Json(new { Result = "OK", name = name });
            }
            catch (Exception ex)
            {
                return Json(new { Result = "ERROR", Message = ex.Message });
            }
        }

        [OutputCache(Duration = 0, VaryByParam = "*", NoStore = true)]
        public ActionResult ViewSkill(string user, string skill_type)
        {
            if (user != "")
            {
                var query = from a in dbTooldie.td_user_skill
                            where a.user_id == user && a.skill_type == skill_type
                            select a;

                if (query != null)
                    return Json(query, JsonRequestBehavior.AllowGet);
                else
                    return Json(0, JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(0, JsonRequestBehavior.AllowGet);
            }
        }

        [OutputCache(Duration = 0, VaryByParam = "*", NoStore = true)]
        public ActionResult ViewRank(string user)
        {
            if (user != "")
            {
                var query = (from a in dbTooldie.tm_user_id
                             where a.user_id == user
                             select a.rank).FirstOrDefault();

                if (query != null)
                    return Json(query, JsonRequestBehavior.AllowGet);
                else
                    return Json(0, JsonRequestBehavior.AllowGet);
            }
            else
            {
                return Json(0, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public ActionResult _ShowSkill(string user, string username)
        {
            var query = from a in dbTooldie.td_user_skill.Where(w => w.user_id == user)
                        join c in dbTooldie.tr_process on a.proc_code equals c.proc_code into p
                        from c in p.DefaultIfEmpty()
                        select new
                        {
                            a.user_id,
                            a.proc_code,
                            c.proc_name,
                            a.evaluation,
                            a.skill_type
                        };

            List<VM_user_skill> vobj = new List<VM_user_skill>();
            VM_user_skill vm;

            foreach (var item in query)
            {
                vm = new VM_user_skill();
                vm.user_id = item.user_id;
                vm.proc_code = item.proc_code;
                vm.proc_name = item.proc_name;
                vm.evaluation = item.evaluation;
                vm.skill_type = item.skill_type == "PC" ? "Process" : "Knowledge";
                vobj.Add(vm);
            }

            var rank = (from a in dbTooldie.tm_user_id
                        where a.user_id == user
                        select a.rank).FirstOrDefault();
            //var query = dbTooldie.td_user_skill.Where(w => w.user_id == user);
            //ViewBag.Skill = query;
            ViewBag.Image = "http://webinternal/lmd/emppic/picstorge/" + user + ".jpg";
            ViewBag.Username = username;
            ViewBag.Rank = rank != null ? rank : "-";
            return PartialView(vobj);
        }

        [HttpGet]
        public ActionResult _AddComment(string mainjob, string subjob, int marking_step)
        {
            var query = (from a in dbTooldie.td_comment
                         where a.main_job_no == mainjob && a.sub_job_no == subjob && a.marking_step == marking_step
                         select a).FirstOrDefault();
            ViewBag.Comment = query != null ? query.comment_process.Trim() : "";
            ViewBag.MainJob = mainjob;
            ViewBag.SubJob = subjob;
            ViewBag.Marking = marking_step;

            return PartialView();
        }

        [HttpPost]
        public ActionResult AddComment()//string main, string sub, int mark, string comment)
        {
            string main = Request.Form["hdMain"];
            string sub = Request.Form["hdSub"];
            int mark = int.Parse(Request.Form["hdMark"].ToString());
            string comment = Request.Form["txaComment"];
            try
            {
                bool exist = dbTooldie.td_comment.Any(w => w.main_job_no == main && w.sub_job_no == sub && w.marking_step == mark);

                if (exist)//Update
                {
                    var update = dbTooldie.td_comment.Find(main, sub, mark);
                    update.comment_process = comment;
                    dbTooldie.SaveChanges();
                }
                else//Add
                {
                    var add = new td_comment();
                    add.main_job_no = main;
                    add.sub_job_no = sub;
                    add.marking_step = mark;
                    add.comment_process = comment;
                    dbTooldie.td_comment.Add(add);
                    dbTooldie.SaveChanges();
                }
                //return Json(new { Result = "Successful." });
                return RedirectToAction("Chk_WO", new { mainjob = main, subjob = sub });
            }
            catch (Exception)
            {
                //return Json(new { Result = "ERROR" + ex.Message });
                return RedirectToAction("Chk_WO", new { mainjob = main, subjob = sub });
            }
        }

        [HttpPost]
        public ActionResult DelComment()//string main, string sub, int mark, string comment)
        {
            string main = Request.Form["hdMain"];
            string sub = Request.Form["hdSub"];
            int mark = int.Parse(Request.Form["hdMark"].ToString());
            try
            {
                var del = dbTooldie.td_comment.Find(main, sub, mark);
                dbTooldie.td_comment.Remove(del);
                dbTooldie.SaveChanges();

                //return Json(new { Result = "Successful." });
                return RedirectToAction("Chk_WO", new { mainjob = main, subjob = sub });
            }
            catch (Exception)
            {
                //return Json(new { Result = "ERROR" + ex.Message });
                return RedirectToAction("Chk_WO", new { mainjob = main, subjob = sub });
            }
        }

        [HttpGet]
        public ActionResult _ViewComment(string mainjob, string subjob, int marking_step)
        {
            var query = (from a in dbTooldie.td_comment
                         where a.main_job_no == mainjob && a.sub_job_no == subjob && a.marking_step == marking_step
                         select a).FirstOrDefault();

            return PartialView(query);
        }

        [HttpPost]
        public ActionResult DelJob(string mainjob, string subjob, int marking_step)
        {
            try
            {
                var del = dbTooldie.tr_job_progress.Find(mainjob, subjob, marking_step);

                var dt = DateTime.Now;
                tr_job_progress_log log = new tr_job_progress_log();
                log.main_job_no = del.main_job_no;
                log.sub_job_no = del.sub_job_no;
                log.marking_step = del.marking_step;
                log.process_code = del.process_code;
                log.qty = del.qty;
                log.machine_no = del.machine_no;
                log.entry_date = del.entry_date;
                log.entry_time = del.entry_time;
                log.update_date = del.update_date.Trim();
                log.update_time = del.update_time.Trim();
                log.delete_date = dt.ToString("yyyyMMdd");
                log.delete_time = dt.ToString("HHmmss");
                dbTooldie.tr_job_progress_log.Add(log);

                dbTooldie.tr_job_progress.Remove(del);
                dbTooldie.SaveChanges();
                return Json(new { Result = "OK" });
            }
            catch (Exception ex)
            {
                return Json(new { Result = "ERROR : " + ex.Message });
            }
        }

        public ActionResult SkillMaster()
        {
            return View();
        }

        [HttpPost]
        public JsonResult SkillList(int jtStartIndex = 0, int jtPageSize = 0, string jtSorting = null)
        {
            try
            {
                var query = dbTooldie.tr_process;

                //Get data from database
                int TotalRecord = query.Count();

                // Paging
                var output = query
                    .Select(s => new
                    {
                        s.proc_code,
                        s.proc_name,
                        s.update_date,
                        s.skill_type,
                        s.standard_time,
                        s.del_flag
                    }).OrderBy(jtSorting).Skip(jtStartIndex).Take(jtPageSize);

                //Return result to jTable
                return Json(new { Result = "OK", Records = output, TotalRecordCount = TotalRecord });
            }
            catch (Exception ex)
            {
                return Json(new { Result = "ERROR", Message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult CreateSkill()
        {
            try
            {
                var formData = Request.Form["proc_code"].ToString();
                var dbData = dbTooldie.tr_process.Where(w => w.proc_code == formData).FirstOrDefault();
                if (dbData == null)
                {
                    var dt = DateTime.Now;
                    tr_process data = new tr_process();
                    data.proc_code = formData;
                    data.proc_name = Request.Form["proc_name"].ToString();
                    data.skill_type = Request.Form["skill_type"].ToString();
                    data.entry_date = dt.ToString("yyyyMMdd");
                    data.entry_time = dt.ToString("hhmmss");
                    data.standard_time = Request.Form["standard_time"].ToString();
                    data.del_flag = 0;
                    //data.skip_process_setup = 0;

                    dbTooldie.tr_process.Add(data);
                }

                dbTooldie.SaveChanges();

                return Json(new { Result = "OK", Record = dbTooldie.tr_process.OrderByDescending(o => o.update_date).FirstOrDefault() });
            }
            catch (Exception ex)
            {
                return Json(new { Result = "ERROR", Message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult UpdateSkill()
        {
            try
            {
                var dt = DateTime.Now;
                var id = Request.Form["proc_code"].ToString();
                var data = dbTooldie.tr_process.Find(id);
                data.proc_name = Request.Form["proc_name"].ToString();
                data.skill_type = Request.Form["skill_type"].ToString();
                data.standard_time = Request.Form["standard_time"].ToString();
                data.update_date = dt.ToString("yyyyMMdd");
                data.update_time = dt.ToString("hhmmss");
                data.del_flag = int.Parse(Request.Form["del_flag"].ToString());
                dbTooldie.SaveChanges();

                return Json(new { Result = "OK" });
            }
            catch (Exception ex)
            {
                return Json(new { Result = "ERROR", Message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult DeleteSkill()
        {
            try
            {
                var dt = DateTime.Now;
                var id = Request.Form["proc_code"].ToString();
                var data = dbTooldie.tr_process.Find(id);
                data.update_date = dt.ToString("yyyyMMdd");
                data.update_time = dt.ToString("hhmmss");
                data.del_flag = 0;
                //dbTooldie.tr_process.Remove(data);
                dbTooldie.SaveChanges();

                return Json(new { Result = "OK" });
            }
            catch (Exception ex)
            {
                return Json(new { Result = "ERROR", Message = ex.Message });
            }
        }
    }
}
