using System;
using System.Net.Http;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace TTS
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await openDocument(args[0]);
        }
    
        static async Task openDocument(string filename)
        {
            string text = File.ReadAllText(filename).Replace("’","'");
            
            if (text.Length > 4000) {

                string[] sentences = text.Split(".");
                StringBuilder bitsOfText = new StringBuilder();
                int counter = 0;


                foreach (string parts in sentences)
                {
                    if ((bitsOfText.Length + parts.Length) > 4000 ){
                        await SayAndDownload(bitsOfText.ToString(), counter.ToString() + filename, "en-US, JessaNeural");
                        bitsOfText.Clear();
                        counter++;
                    }
                    bitsOfText.Append(parts);
                    bitsOfText.Append(". ");
                }
                await SayAndDownload(bitsOfText.ToString(), counter.ToString() + filename, "en-US, JessaNeural");
            } else {
                await SayAndDownload(text, filename);
            }
        }

        static async Task SayAndDownload(string text, string outfilename, string voice = "en-US, GuyNeural")
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();
           // Gets an access token
            string accessToken;
            Console.WriteLine("Attempting token exchange. Please wait...\n");

            string host = configuration.GetConnectionString("host");
            string endpoint = configuration.GetConnectionString("Auth_endpoint");
            string APIKey = configuration.GetConnectionString("Azure_TTS_API_Key");


            Authentication auth = new Authentication(endpoint, APIKey);
            try
            {
                accessToken = await auth.FetchTokenAsync().ConfigureAwait(false);
                Console.WriteLine("Successfully obtained an access token. \n");

                string body = @"<speak version='1.0' xmlns='https://www.w3.org/2001/10/synthesis' xml:lang='en-US'>
                     <voice name='Microsoft Server Speech Text to Speech Voice (" + voice + ")'>" +
                    text + "</voice></speak>";

                using (var client = new HttpClient())
                {
                    using (var request = new HttpRequestMessage())
                    {
                        // Set the HTTP method
                        request.Method = HttpMethod.Post;
                        // Construct the URI
                        request.RequestUri = new Uri(host);
                        // Set the content type header
                        request.Content = new StringContent(body, Encoding.UTF8, "application/ssml+xml");
                        // Set additional header, such as Authorization and User-Agent
                        request.Headers.Add("Authorization", "Bearer " + accessToken);
                        request.Headers.Add("Connection", "Keep-Alive");
                        // Update your resource name
                        request.Headers.Add("User-Agent", "Radical Audio TTS");
                        request.Headers.Add("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");
                        // Create a request
                        Console.WriteLine("Calling the TTS service. Please wait... \n");
                        using (var response = await client.SendAsync(request).ConfigureAwait(false))
                        {
                            response.EnsureSuccessStatusCode();
                            // Asynchronously read the response
                            using (var dataStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            {
                                Console.WriteLine("Your speech file is being written to file...");
                                using (var fileStream = new FileStream(outfilename + ".wav", FileMode.Create, FileAccess.Write, FileShare.Write))
                                {
                                    await dataStream.CopyToAsync(fileStream).ConfigureAwait(false);
                                    fileStream.Close();
                                }
                                Console.WriteLine("\nYour file " + outfilename + ".wav" + " is ready.");
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to obtain an access token.");
                Console.WriteLine(ex.ToString());
                Console.WriteLine(ex.Message);
                return;
            }
        }


    }
}