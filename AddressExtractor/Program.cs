using AngleSharp;
using AngleSharp.Parser.Html;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AddressExtractor
{
    class Program
    {
        static string baseUrl = "https://www.xo.gr/dir-tk";
        static string baseUrlPrefectureAreas = "https://www.xo.gr";
        static string baseUrlAddress = "https://www.xo.gr";
        private static readonly HttpClient Client = new HttpClient();

        [STAThread]
        static void Main(string[] args)
        {
                MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("Reading Prefectures");
            var prefectures = await ReadPrefectureList();

            Console.WriteLine("Reading Zip Code Areas");
            var allZipCodeAreas = await ReadAllPrefectureZipCodeAreas(prefectures);

            // Continue from thessaloniki and after
            var wantedElement = allZipCodeAreas.FindIndex(x => x.Equals("/taxydromikos-kodikas-tk/Thessaloniki/"));
            allZipCodeAreas = allZipCodeAreas.GetRange(wantedElement, allZipCodeAreas.Count-wantedElement);

            Console.WriteLine("Reading Addresses");
            var allAddresses = await ReadAllAddresses(allZipCodeAreas);

            Console.WriteLine("Formatting to JSON");
            var resultString = JsonConvert.SerializeObject(allAddresses);

            Console.WriteLine("Writing to file");
            File.WriteAllText(@"C:\address.json", resultString);

            Console.WriteLine("Done.");
            Console.ReadLine();
        }

        private static async Task<List<string>> ReadPrefectureList()
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

        private static async Task<List<string>> ReadAllPrefectureZipCodeAreas(List<string> prefectures)
        {
            var allZipCodeAreas = new List<string>();

            // Setup the configuration to support document loading
            var config = Configuration.Default.WithDefaultLoader();

            // This CSS selector gets the desired content
            var cellSelector = "ul[class='unorderedGeoPrefLoc span4']>li>a";

            foreach (var prefecture in prefectures)
            {
                List<string> tempZipCodeAreas;

                var counter = 1;

                do
                {
                    var url = $"{baseUrlPrefectureAreas}{prefecture}?page={counter}";

                    Console.WriteLine(prefecture);

                    var document = await BrowsingContext.New(config).OpenAsync(url);

                    // Perform the query to get all cells with the content
                    var cells = document.QuerySelectorAll(cellSelector);

                    tempZipCodeAreas = cells.Select(m => m.GetAttribute("href")).ToList();

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

                var dataWhat = dataWhatCell?.GetAttribute("data-what");

                if (dataWhat == null)
                {
                    continue;
                }

                var pageCounter = 1;

                List<Address> addresses;

                do
                {
                    // XO.gr thinks that i am bot and blocks my ip
                    // Adding sleep to simulate human behaviour. Lets see if it works
                    Thread.Sleep(1200);

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

                    pageCounter++;
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
                    {"mode", "region"},
                    {"Query", $"{dataWhat}"},
                    {"Page", $"{page}"},
                    {"LanguageID", $"{language}"},
                    {"Filter", "name"},
                    {"Order", "asc"}
                };

                var content = new FormUrlEncodedContent(values);

                var response = await Client.PostAsync("https://www.xo.gr/AjaxView/AjaxZipCode", content);

                var responseString = await response.Content.ReadAsStringAsync();

                return responseString;
            }
            catch (Exception e)
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
            const string lineSelector = "table[id='zipTable']>tbody>tr";

            var document = parser.Parse(response);

            var addressLines = document.QuerySelectorAll(lineSelector);

            if (addressLines == null || !addressLines.Any())
            {
                return new List<Address>();
            }

            foreach (var addressLine in addressLines)
            {
                const string addressPartSelector = "td";

                var addressParts = addressLine.QuerySelectorAll(addressPartSelector);

                if (addressParts == null || !addressParts.Any())
                {
                   continue;
                }

                var temp = new Address()
                {
                    Name = addressParts[0].TextContent,
                    ZipCode = addressParts[1].TextContent,
                    CityArea = addressParts[2].TextContent,
                    Prefecture = addressParts[3].TextContent,
                };

                addresses.Add(temp);

                Console.WriteLine($"{temp.Name}, {temp.ZipCode}, {temp.CityArea}, {temp.Prefecture}");
            }

            return addresses;
        }
    }
}