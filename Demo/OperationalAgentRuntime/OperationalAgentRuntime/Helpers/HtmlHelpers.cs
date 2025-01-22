using OperationalAgentRuntime.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentRuntime.Helpers
{
    public static class HtmlHelpers
    {
        public static string GenerateHtmlTableForBasicAuth(List<BasicAuthStatus> list)
        {
            StringBuilder html = new StringBuilder();

            // Start table  
            html.Append("<br /><br /><table border='1'>");

            // Table header  
            html.Append("<tr>");
            html.Append("<th>App Name</th>");
            html.Append("<th>Scm Basic Auth Allowed</th>");
            html.Append("<th>FTP Basic Auth Allowed</th>");
            html.Append("</tr>");

            // Table rows  
            foreach (var item in list)
            {
                string scmColor = item.ScmBasicAuthAllowed ? "#dc3545" : "#28a745";
                string ftpColor = item.FtpBasicAuthAllowed ? "#dc3545" : "#28a745";

                html.Append("<tr>");
                html.AppendFormat("<td>{0}</td>", item.Name);
                html.Append($"<td><span style='color:{scmColor}'>{item.ScmBasicAuthAllowed}</span></td>");
                html.Append($"<td><span style='color:{ftpColor}'>{item.FtpBasicAuthAllowed}</span></td>");
                html.Append("</tr>");
            }

            // End table  
            html.Append("</table>");

            return html.ToString();
        }

        public static string GenerateHtmlTableForAppSku(AppPlanSku sku)
        {
            StringBuilder html = new StringBuilder();

            // Start table  
            html.Append("<br /><br /><table border='1'>");

            // Table header  
            html.Append("<tr>");
            html.Append("<th>Plan SKU</th>");
            html.Append("<th>Number of Instances</th>");
            html.Append("</tr>");
            
            html.Append("<tr>");
            html.Append($"<td>{sku.Tier} - {sku.Size}</td>");
            html.Append($"<td>{sku.Capacity}</td>");
            html.Append("</tr>");
           
            // End table  
            html.Append("</table>");

            return html.ToString();
        }

        public static string GenerateHtmlTableForList(List<string> list)
        {
            StringBuilder html = new StringBuilder();

            // Start table  
            html.Append("<br /><br /><table border='1'>");

            // Table header  
            html.Append("<tr>");
            html.Append("<th>App Services</th>");
            html.Append("</tr>");

            // Table rows  
            foreach (var item in list)
            {
                html.Append("<tr>");
                html.AppendFormat("<td>{0}</td>", item);
                html.Append("</tr>");
            }

            // End table  
            html.Append("</table>");

            return html.ToString();
        }
    }
}
