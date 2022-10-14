using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
//using Microsoft.Identity.Client.Desktop;

namespace DefenderBug
{
    internal class Program
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        private const string UserCacheFile = "msal_user_cache.json";

        static async Task Main(string[] args)
        {
            IntPtr _parentHandle = GetForegroundWindow();
            Func<IntPtr> consoleWindowHandleProvider = () => _parentHandle;

            // 1. Configuration - read below about redirect URI
            var pca = PublicClientApplicationBuilder.Create("04f0c124-f2bc-4f59-8241-bf6df9866bbd")
                          .WithAuthority("https://login.microsoftonline.com/*", validateAuthority: false)
                          .WithRedirectUri("http://localhost")
                          //.WithWindowsBroker()
                          .WithBrokerPreview()
                          .WithWindowsBrokerOptions(new WindowsBrokerOptions()
                          {
                              // GetAccounts will return Work and School accounts from Windows
                              ListWindowsWorkAndSchoolAccounts = false,

                              // Legacy support for 1st party apps only
                              MsaPassthrough = true
                          })
                          .WithParentActivityOrWindow(consoleWindowHandleProvider)
                          .Build();

            BindCache(pca.UserTokenCache, UserCacheFile);

            // Add a token cache, see https://docs.microsoft.com/en-us/azure/active-directory/develop/msal-net-token-cache-serialization?tabs=desktop

            // 2. GetAccounts
            var accounts = await pca.GetAccountsAsync();
            var accountToLogin = accounts.FirstOrDefault();

            try
            {
                // 3. AcquireTokenSilent 

                var authResult = await pca.AcquireTokenSilent(new[] { "user.read" } , accountToLogin)
                                      .ExecuteAsync();

                Console.WriteLine(authResult.Account);

                Console.WriteLine("Done!!!");

                Console.Read();
            }
            catch (MsalUiRequiredException) // no change in the pattern
            {


                // 5. AcquireTokenInteractive
                var authResult = await pca.AcquireTokenInteractive(new[] { "user.read" })
                                          //.WithAccount(accountToLogin)  // this already exists in MSAL, but it is more important for WAM
                                          .WithParentActivityOrWindow(consoleWindowHandleProvider) // to be able to parent WAM's windows to your app (optional, but highly recommended; not needed on UWP)
                                          .ExecuteAsync();

                Console.WriteLine(authResult.Account);

                ////logout
                //IEnumerable<IAccount> account = await pca.GetAccountsAsync();
                //if (account.Any())
                //{
                //    try
                //    {
                //        await pca.RemoveAsync(accounts.FirstOrDefault());
                //    }
                //    catch (MsalException ex)
                //    {
                //        Console.WriteLine($"MsalException Error signing-out user: {ex.Message}");
                //    }
                //    catch (Exception ex)
                //    {
                //        Console.WriteLine($"Exception Error signing-out user: {ex.Message}");
                //    }
                //}

                Console.Read();
            }
        }

        private static void BindCache(ITokenCache tokenCache, string file)
        {
            tokenCache.SetBeforeAccess(notificationArgs =>
            {
                notificationArgs.TokenCache.DeserializeMsalV3(File.Exists(file)
                    ? File.ReadAllBytes(UserCacheFile)
                    : null);
            });

            tokenCache.SetAfterAccess(notificationArgs =>
            {
                // if the access operation resulted in a cache update
                if (notificationArgs.HasStateChanged)
                {
                    // reflect changes in the persistent store
                    File.WriteAllBytes(file, notificationArgs.TokenCache.SerializeMsalV3());
                }
            });
        }
    }
}
