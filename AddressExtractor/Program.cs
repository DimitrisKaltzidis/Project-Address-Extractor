using AngleSharp;
using AngleSharp.Parser.Html;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AddressExtractor
{
    class Program
    {
        static string baseUrl = "https://www.xo.gr/dir-tk";
        static string baseUrlPrefectureAreas = "https://www.xo.gr";
        static string baseUrlAddress = "https://www.xo.gr";
        private static readonly HttpClient client = new HttpClient();

        [STAThread]
        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            var perfectures = await ReadPerfectureList();

            var allZipCodeAreas = await ReadAllPerfectureZipCodeAreas(perfectures);

            var allAddresses = await ReadAllAddresses(allZipCodeAreas);
        }

        private static async Task<List<string>> ReadPerfectureList()
        {
            // Setup the configuration to support document loading
            var config = Configuration.Default.WithDefaultLoader();

            // Asynchronously get the document in a new context using the configuration
            var document = await BrowsingContext.New(config).OpenAsync(baseUrl);

            // This CSS selector gets the desired content
            var cellSelector = "ul[class='span3']>li>a";

            // Perform the query to get all cells with the content
            var cells = document.QuerySelectorAll(cellSelector);

            // We are only interested in the text - select it with LINQ
            return cells.Select(m => m.GetAttribute("href")).ToList();
        }

        private static async Task<List<string>> ReadAllPerfectureZipCodeAreas(List<string> perfectures)
        {
            var allZipCodeAreas = new List<string>();

            // Setup the configuration to support document loading
            var config = Configuration.Default.WithDefaultLoader();

            // This CSS selector gets the desired content
            var cellSelector = "ul[class='unorderedGeoPrefLoc span4']>li>a";

            foreach (var perfecture in perfectures)
            {
                var tempZipCodeAreas = new List<string>();

                var counter = 1;

                do
                {
                    var url = $"{baseUrlPrefectureAreas}{perfecture}?page={counter}";

                    Console.WriteLine(perfecture);

                    var document = await BrowsingContext.New(config).OpenAsync(url);

                    // Perform the query to get all cells with the content
                    var cells = document.QuerySelectorAll(cellSelector);

                    tempZipCodeAreas = cells.Select(m => m.GetAttribute("href")).ToList();

                    if (tempZipCodeAreas == null)
                    {
                        break;
                    }

                    allZipCodeAreas.AddRange(tempZipCodeAreas);

                    counter++;

                } while (tempZipCodeAreas.Any());
            }

            return allZipCodeAreas;
        }

        private static async Task<List<Address>> ReadAllAddresses(List<string> allZipCodeAreas)
        {
            // Load page and extract data-what attribute table area to get the needed code
            // Setup the configuration to support document loading
            var config = Configuration.Default.WithDefaultLoader();

            var allAddresses = new List<Address>();

            foreach (var zipCodeArea in allZipCodeAreas)
            {

                var url = $"{baseUrlAddress}{zipCodeArea}";

                var document = await BrowsingContext.New(config).OpenAsync(url);

                // This CSS selector gets the desired content
                var cellSelector = "div[id='contentTable']>table[id='zipTable'] th:first-child";

                var dataWhatCell = document.QuerySelector(cellSelector);

                if (dataWhatCell == null)
                {
                    continue;
                }

                var dataWhat = dataWhatCell.GetAttribute("data-what");

                if (dataWhat == null)
                {
                    continue;
                }

                var pageCounter = 1;

                var addresses = new List<Address>();

                do
                {
                    // Fetch response page
                    var response = await PostAsync(dataWhat, pageCounter);

                    if (response == null)
                    {
                        break;
                    }

                    addresses = ParseAddressResponse(response);

                    if (addresses.Any())
                    {
                        allAddresses.AddRange(addresses);
                    }

                } while (addresses.Any());
            }

            return allAddresses;
        }

        private static async Task<string> PostAsync(string dataWhat, int page, int language = 1)
        {
            try
            {
                var values = new Dictionary<string, string>
            {
               { "mode", "region" },
               { "Query", $"{dataWhat}" },
               { "Page", $"{page}" },
               { "LanguageID", $"{language}" },
               { "Filter", "name" },
               { "Order", "asc" }
            };

                var content = new FormUrlEncodedContent(values);

                var response = await client.PostAsync("https://www.xo.gr/AjaxView/AjaxZipCode", content);

                var responseString = await response.Content.ReadAsStringAsync();

                return responseString;
            }catch(Exception e)
            {
                Console.WriteLine(e.StackTrace);
                return null;
            }
        }

        private static List<Address> ParseAddressResponse(string response)
        {
            var parser = new HtmlParser();

            var addresses = new List<Address>();

            // Line selector
            var lineSelector = "table[id='zipTable']>tbody tr";

            var document = parser.Parse(response);

            var addressRows = document.QuerySelectorAll(lineSelector);

            foreach(var row in addressRows)
            {
                var childern = row.ChildNodes;

                var temp = new Address()
                {
                    Name = childern[0].TextContent,
                    ZipCode = childern[1].TextContent,
                    CityArea = childern[2].TextContent,
                    Prefecture = childern[3].TextContent,
                };

                addresses.Add(temp);

                Console.WriteLine($"{temp.Name}, {temp.ZipCode}, {temp.CityArea}, {temp.Prefecture}");
            }

            return addresses;
        }
    }
}
