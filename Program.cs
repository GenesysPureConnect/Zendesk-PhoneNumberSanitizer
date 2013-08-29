using System;
using ZendeskApi_v2;
using System.Threading;
using System.Text.RegularExpressions;

namespace Zendesk_PhoneNumberSanitizer
{
    class Program
    {
        const int RequestsPerMinute = 180; //api doc says max 200 requests per minute, we'll scale back from that a little bit just in case.   
        const int OneMinute = 60000;
        
        private static int s_apiCount = 0;
        private static AutoResetEvent s_apiLimitResetEvent = new AutoResetEvent(false);

        static void Main(string[] args)
        {
            Console.Clear();

            if (args == null || args.Length < 3)
            {
                Console.WriteLine("usage is ");
                Console.WriteLine();
                Console.WriteLine("ZendeskPhoneNumberSanitizer.exe ZENDESKURL USERNAME PASSWORD");
                Console.WriteLine();
                Console.WriteLine("ZENDESKURL should be in the format https://yoursite/api/v2");
                Console.WriteLine("User name and password are for a user that has rights to update records in zendesk");
                Console.WriteLine();
            }
            else
            {
                try
                {
                    var api = Connect(args[0], args[1], args[2]);
                    UpdateUsers(api);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: " + ex.Message);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to quit...");
            Console.Read();
        }

        private static ZendeskApi Connect(string serverUrl, string userName, string password)
        {
            Console.WriteLine(String.Format("Attempting to connect to Zendesk with user: '{0}' at url: {1}", userName, serverUrl));
            var api = new ZendeskApi(serverUrl, userName, password);
            return api;
        }

        private static void UpdateUsers(ZendeskApi api)
        {
            var rateTimer = new Timer(new TimerCallback(RateTimerCallback), null, OneMinute, OneMinute);

            var allUsers = api.Users.GetAllUsers().Users;
            foreach (var user in allUsers)
            {
                var sanitizedNumber = SanitizeNumber(user.Phone);
                if (user.Phone != sanitizedNumber)
                {
                    Console.WriteLine(String.Format("Updating {0} {1} to {2}", user.Name, user.Phone, sanitizedNumber));
                    
                    user.Phone = sanitizedNumber;
                    api.Users.UpdateUser(user);
                    
                    s_apiCount++;

                    if (s_apiCount >= RequestsPerMinute)
                    {
                        Console.Write("Api limit reached, waiting for timer to expire...");
                        s_apiLimitResetEvent.WaitOne();
                        Console.WriteLine("Done");
                    }
                }
            }
        }

        private static void RateTimerCallback(object state)
        {
            //potential race condition here between threads.  I'm not too concerned about it as it will clear itself out
            s_apiCount = 0;
            s_apiLimitResetEvent.Set();
        }

        private static string SanitizeNumber(string number)
        {
            if (String.IsNullOrEmpty(number))
            {
                return number;
            }
            return Regex.Replace(number, "\\D", String.Empty);
        }
    }
}
