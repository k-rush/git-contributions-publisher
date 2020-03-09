using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Web;
using Octokit.GraphQL;
using System.Text;
using System.Linq;

namespace github_contributions_publisher
{
    public static class Function1
    {
        [FunctionName("GetOAuthToken")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "auth")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string code = req.Query["code"];

            var builder = new UriBuilder("https://github.com/login/oauth/access_token");
            var query = HttpUtility.ParseQueryString(builder.Query);
            query["client_id"] = "CLIENT_ID";
            query["client_secret"] = "CLIENT_SECRET";
            query["code"] = code;
            builder.Query = query.ToString();
            HttpClient client = new HttpClient();
            var response = await client.PostAsync(builder.ToString(), new StringContent(""));
            var stringResponse = await response.Content.ReadAsStringAsync();
            log.LogInformation(stringResponse);

            return new OkObjectResult(stringResponse);
        }


        [FunctionName("GetContributions")]
        public static async Task<IActionResult> GetContributions(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "contributions")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            var productInformation = new ProductHeaderValue("YOUR_PRODUCT_NAME", "YOUR_PRODUCT_VERSION");
            var connection = new Connection(productInformation, "TOKEN");
            var query = new Query()
                .User("kdr213")
                .ContributionsCollection()
                .ContributionCalendar
                .Weeks.Select(week => 
                    new { 
                        week.FirstDay, 
                        Days = week.ContributionDays.Select(day => 
                            new { 
                                day.ContributionCount, 
                                day.Date, 
                                day.Weekday 
                            }).ToList() 
                    });
            var result = await connection.Run(query);
            var lastFourWeeks = result.Skip(Math.Max(0, result.Count() - 2));
            double[] contributions = lastFourWeeks.SelectMany(week => week.Days.Select(day => (double)day.ContributionCount)).ToArray(); ;
            DateTime start = DateTime.Parse(lastFourWeeks.FirstOrDefault()?.Days.FirstOrDefault()?.Date);
            var plt = new ScottPlot.Plot(600, 200);
            plt.PlotSignal(contributions, sampleRate: 1, xOffset: start.ToOADate());
            plt.Ticks(dateTimeX: true);
            plt.YLabel("Contributions");
            plt.XLabel("Date");

            plt.Style(ScottPlot.Style.Blue1);
            //plt.SaveFig("12_Date_Axis.png");
            var bmp = plt.GetBitmap();
            

            var resultBuilder = new StringBuilder();
            foreach(var contributionWeek in result)
            {
                foreach(var contributionDay in contributionWeek.Days)
                {
                    var dayLog = $"Contriubtions on {contributionDay.Date}: {contributionDay.ContributionCount}";
                    resultBuilder.AppendLine(dayLog);
                    log.LogInformation(dayLog);
                }
            }

            return new OkObjectResult(resultBuilder.ToString());
        }
        

    }
}
