﻿using ASO.Areas.SysAuth.Models;
using ASO.Areas.WoodLand.Models;
using ASO.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using Wei.SysAuth;
using WeiCommon;
//using ASO.Areas.WoodLand.Models;

namespace ASO.Areas.WoodLand.Controllers
{
    public class LoginController : Controller
    {
        public ActionResult Error()
        {
            return View();
        }
        // GET: WoodLand/Login
        public ActionResult Login(string returnUrl)
        {
            List<object> ret = new List<object>();
            ret.Add(GetDeptList());
            ret.Add(SysApp.AuthMgn.GetRegControl().ReturnData.Item1);
            ViewBag.ReturnUrl = returnUrl;

            return PartialView("LoginShared", ret);
        }

        // GET: WoodLand/LoginPwd
        public ActionResult LoginPwd(string returnUrl)
        {
            List<object> ret = new List<object>();

            return PartialView("LoginSharedPwd", ret);
        }

        // GET: WoodLand/OperatingInstructions
        public ActionResult LoginOperatingInstructions(string returnUrl)
        {
            List<object> ret = new List<object>();

            return PartialView("LoginOperatingInstructions", ret);
        }

        [HttpPost]
        public ActionResult showpassword(string _account, string _password)
        {
            string _result = string.Empty;
            try
            {
                ReturnObject<SysUser> userObj = SysApp.AuthMgn.GetSysUserBy(-1, _account);
                // 裡面有帳號各種方法。判斷密碼、等等，也可以取得資訊。
                SysUser usr = userObj.ReturnData;
                clsWLMth mth = new clsWLMth();
                string[] LoginError = mth.GetLoginError(_account);
                int ErrorCount = Convert.ToInt32(LoginError[0]);
                DateTime UnLockTime = !string.IsNullOrEmpty(LoginError[1]) ? Convert.ToDateTime(LoginError[1]) : DateTime.MinValue;
                usr.PasswordDecode();
                // usr.PasswordEncode(); 反之。這個加密
                string password = usr.Password;
                if (UnLockTime > DateTime.Now)
                {
                    return Content("Lock");
                }
                if (password == _password)
                {
                    _result = "OK";
                }
                else
                {
                    _result = "NoPASS";
                    mth.PlusLoginErrors(_account);
                    if (ErrorCount == 2)
                    {
                        mth.LoginErrorLock(_account);
                        return Content("Lock");
                    }
                }
            }
            catch (Exception)
            {
                _result = "NoPASS";
            }

            return Content(_result);
        }

        [HttpPost]
        public ActionResult ClearCookie()
        {
            FormsAuthentication.SignOut();
            Session.Abandon();

            HttpCookie cookie1 = new HttpCookie(FormsAuthentication.FormsCookieName, "");
            cookie1.HttpOnly = true;
            cookie1.Expires = DateTime.Now.AddYears(-1);
            Response.Cookies.Add(cookie1);

            HttpCookie cookie2 = new HttpCookie("ASP.NET_SessionId", "");
            cookie2.HttpOnly = true;
            cookie2.Expires = DateTime.Now.AddYears(-1);
            Response.Cookies.Add(cookie2);
            return Content("OKOK");
        }

        [HttpPost]
        public ActionResult ValidateLogin(string acc, string pwd, string ReturnUrl, string urlShared)
        {
            acc = acc.Replace("'", "");
            // 原本的 帳號密碼驗證被弄成 BIN 檔，這裡額外處理登入問題。
            ReturnObject<SysUser> userObj = SysApp.AuthMgn.GetSysUserBy(-1, acc);
            if (userObj == null || userObj.ReturnValue == OpReturnValue.NoData)
                TempData["ErrorMsg"] = "帳號不存在。";
            else if (userObj.ReturnValue == OpReturnValue.Exception)
                TempData["ErrorMsg"] = userObj.ReturnMessage;
            else if (userObj.ReturnData.Status == enSysUserStatus.UNCONFIRMED)
                TempData["ErrorMsg"] = "帳號待確認中。";
            else if (userObj.ReturnData.Status == enSysUserStatus.DISABLE)
                TempData["ErrorMsg"] = "帳號無法使用。";
            else if (!userObj.ReturnData.isPasswordCorrect(pwd))
                TempData["ErrorMsg"] = "密碼錯誤。";
            else if (userObj.ReturnData.isPasswordCorrect(pwd))
            {
                List<object> obj = new List<object>();
                List<int> funs = SysTool.GetUserAllFun(userObj.ReturnData);
                obj.Add(userObj.ReturnData);
                obj.Add(funs);
                int mins = int.Parse(System.Configuration.ConfigurationManager.AppSettings["LoginTime"]);
                Session.RemoveAll();
                FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(1,
                    userObj.ReturnData.Name,
                    DateTime.Now,
                    DateTime.Now.AddMinutes(mins),
                    false,
                    JsonConvert.SerializeObject(obj),
                    FormsAuthentication.FormsCookiePath);
                string encTicket = FormsAuthentication.Encrypt(ticket);
                Response.Cookies.Add(new HttpCookie(FormsAuthentication.FormsCookieName, encTicket));
                Session["Name"] = userObj.ReturnData.Name;
                Session["AccountID"] = acc;
                Session["Password"] = pwd;
                Session["SysMgn"] = funs.Any(s => s == (int)enFunc.系統管理);

                if (SysTool.GetWLSysMgn())
                {
                    SysTool.InsertUR(userObj.ReturnData, enFunc.系統管理, enFuncAct.登入);

                    return Redirect(urlShared);
                    //return RedirectToAction("BulletinMgn", "WLSystem", new { area = "WoodLand" });
                }
                else
                {
                    SysTool.InsertUR(userObj.ReturnData, enFunc.平台介紹, enFuncAct.登入);
                    string rUrl = Regex.Replace(Request.Url.Query, @".+=", "").Replace("%2F", "/");

                    if (rUrl != null && rUrl != "")
                    {
                        return Redirect(rUrl);
                    }
                    else
                    {
                        return Redirect(urlShared);
                        //return RedirectToAction("Platform", "About", new { area = "WoodLand" });
                    }
                }
            }
            List<object> ret = new List<object>();
            ret.Add(GetDeptList());
            ret.Add(SysApp.AuthMgn.GetRegControl().ReturnData.Item1);
            return View(ret);
        }

        [AllowAnonymous]
        public string Registered(SysUser user)
        {
            Regex rgx = new Regex(@"^\d{2}_\d{4}");
            bool rgxReg = rgx.IsMatch(user.AccountID);
            if (rgxReg)
            {
                user.TitID = 2;
                user.Status = enSysUserStatus.ENABLE;
            }
            else
            {
                user.TitID = 7;
                user.Status = enSysUserStatus.UNCONFIRMED;
            }
            user.RegTime = DateTime.Now.ToString("yyyy/MM/dd");
            user.RegIP = Server.HtmlEncode(Request.UserHostAddress);
            if (user.Comment == null)
            {
                user.Comment = "";
            }
            //user.Gender = enSysUserGender.MALE;       
            clsWLMth mth = new clsWLMth();
            bool existed = mth.AccountExisted(user.AccountID);
            if (existed)
            {
                return "Existed";
            }


            bool ret = user.DbCreate().ReturnData;
            if (rgxReg && ret)
                mth.LoginErrorReset(user.AccountID);
            SysTool.SentConfirmMail(user, ret);
            return "OK";
        }

        public string AccountExisted(string acc)
        {
            clsWLMth mth = new clsWLMth();
            bool existed = mth.AccountExisted(acc);
            if (existed)
            {
                return "Existed";
            }
            else
                return "OK";
        }

        [AllowAnonymous]
        public string RePwd(string acc, string email)
        {
            string ret = "";
            ReturnObject<SysUser> userObj = SysApp.AuthMgn.GetSysUserBy(-1, acc);

            // 在這裡查找 備分信箱 Email : 

            clsSysAuthMethod mth = new clsSysAuthMethod();
            string _Email = mth.getPersonalEmail(acc);

            if (userObj == null || userObj.ReturnValue == OpReturnValue.NoData)
                ret = "查無此帳號資料";
            else if (userObj.ReturnData.EMail != email && _Email != email)
                ret = "帳號與Email未對應，請重新輸入";
            else
            {
                Random random = new Random();
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnoprqstuvwxyz0123456789@#!%";
                string rPwd = new string(Enumerable.Repeat(chars, 8).Select(s => s[random.Next(s.Length)]).ToArray());
                SysUser usr = userObj.ReturnData;
                usr.Password = rPwd;
                if (usr.DbModify().ReturnData)
                {
                    usr.PasswordDecode();
                    var tmpRegControl = SysApp.AuthMgn.GetRegControl();
                    string subject = "航照判釋知識教育推廣平台密碼查詢結果";
                    string content = string.Format(@"<div>親愛的{0} 您好：</div><br/>", usr.Name);
                    content += string.Format(@"<div>您所查詢的帳號為：{0}</div><br/>", usr.AccountID);
                    content += string.Format(@"<div>密碼：{0}</div><br/><br/>", usr.Password);
                    content += "<div>林務局農林航空測量所  敬上</div><br/>";
                    content += "<div>電話:(02)23332685</div><br/>";
                    content += "<div>地址:100台北市中正區和平西路二段100號</div>";
                    MailAddressCollection mailAddresses = new MailAddressCollection();
                    string BackupEmail = mth.getPersonalBackupEmail(acc);

                    string _senEmail = string.Empty;

                    if (usr.EMail == string.Empty)
                    {
                        _senEmail = _Email;
                    }
                    else
                    {
                        _senEmail = usr.EMail;

                    }

                    mailAddresses.Add(new MailAddress(_senEmail, usr.Name));
                    mailAddresses.Add(new MailAddress(BackupEmail, usr.Name));

                    //mailAddresses.Add(new MailAddress(usr.EMail, usr.Name));
                    string maillog = MailHelpers.SendMail(mailAddresses, null, null, subject, content);
                    ret = "OK";
                }
                else
                    ret = "查詢失敗，請聯絡系統管理者。";
            }
            return ret;
        }

        private object GetDeptList()
        {
            List<Department> depList = SysApp.AuthMgn.GetAllDartmentList();
            var t = SysApp.AuthMgn.GetRegControl();
            Session.RemoveAll();
            if (depList.Count != 0)
            {
                var result = depList.Where(s => s.RowStatus == 1 && s.DeptName != "管理單位").Select(s => new
                {
                    s.DeptID,
                    s.DeptNo,
                    Name = s.DeptName
                });
                if (t.ReturnData.Item1)
                    return (JsonConvert.SerializeObject(result));
                else
                    return (JsonConvert.SerializeObject(result.Where(s => s.DeptNo != "XX")));
            }
            else
                return ("");
        }
    }
}