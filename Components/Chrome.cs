namespace KashinChatBotService
{
	using OpenQA.Selenium;
	using OpenQA.Selenium.Chrome;
	using OpenQA.Selenium.Remote;
	using System;
	using System.Collections.Generic;
	using System.Drawing;
	using System.Drawing.Drawing2D;
	using System.Drawing.Imaging;
	using System.Globalization;
	using System.IO;
	using System.Net.Http;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading;

	public class Chrome
	{ 
		public static string Get(string shortLink, string folder, ChromeDriver driver)
		{
			string driverUrl;
			var fileImage = Path.Combine(folder, ComputeHash(shortLink) + ".png");
			if (File.Exists(fileImage) && new FileInfo(fileImage).Length > 0)
				return fileImage;
			int width = 0, height = 0, x = 0, y = 0;
			try
			{
				if (driver == null)
					throw new Exception("ChromeDriver is null!");
				driver.Navigate().GoToUrl(shortLink + (shortLink.ToLower().Contains("livejournal.com") ? "?embed=1" : string.Empty));
				while (String.IsNullOrWhiteSpace(driver.PageSource))
					Thread.Sleep(100);

				try
				{
					int repeat = 300;
					while (!((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete") && repeat-- > 0)
						Thread.Sleep(100);
				}
				catch (Exception) { }
				try { driver.FindElement(By.TagName("body")).SendKeys(Keys.F11); }
				catch { }
				Thread.Sleep(8000);
				driverUrl = driver.Url.ToLower();
				if (driverUrl.Contains("livejournal.com"))
					try { driver.Manage().Window.Size = new Size(500, driver.Manage().Window.Size.Height); }
					catch { }
				else
					driver.Manage().Window.Maximize();
				if (driverUrl.Contains("facebook.com") && driverUrl.Contains("/videos/"))
				{
					driver.Navigate().GoToUrl(driverUrl.Replace("/videos/", "/posts/"));
					while (String.IsNullOrWhiteSpace(driver.PageSource))
						Thread.Sleep(100);
					driverUrl = driver.Url.ToLower();
				}
				var driverJS = (IJavaScriptExecutor)driver;
				var driverSS = (ITakesScreenshot)driver;
				var jsHide = "arguments[0].style.visibility='hidden'";
				var jsDrop = "arguments[0].style.display='none'";
				//var jsBorder = "arguments[0].style.border='solid 1px red'";
				var jsScroll = "arguments[0].scrollIntoView(true);";
				if (driverUrl.Contains("/t.me/"))
				{
					driver.Navigate().GoToUrl(driverUrl + "?embed=1");
					IWebElement elem = driver.FindElement(By.XPath("//div[@id='widget_message']"));
					driverJS.ExecuteScript(jsScroll, elem);
					width = elem.Size.Width;
					height = elem.Size.Height;
					x = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.X;
					y = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.Y;
				}
				else if (driverUrl.Contains("facebook.com"))
				{
					try { driverJS.ExecuteScript(jsDrop, driver.FindElement(By.Id("pagelet_navigation"))); } catch { }
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.XPath("//a[@aria-label='Story options']"))); } catch { }
					try { driverJS.ExecuteScript(jsDrop, driver.FindElement(By.Id("u_0_c"))); } catch { }
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.Id("headerArea"))); } catch { }
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.XPath("//a[text()='Follow']"))); } catch { }
					var elem = driver.FindElement(By.CssSelector("div.userContent")).FindElement(By.XPath(".."));
					driverJS.ExecuteScript(jsScroll, elem);
					width = elem.Size.Width;
					height = elem.Size.Height;
					x = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.X;
					y = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.Y;
				}
				else if (driverUrl.Contains("vk.com"))
				{

					try
					{
						foreach (var elm in driver.FindElements(By.XPath("//span[contains(@class,'rel_date_needs_update')]")))
						{
							var pubDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)
								.AddSeconds(long.Parse(elm.GetAttribute("time"))).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
							driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
					IWebElement element1 = null;
					try { element1 = driver.FindElement(By.CssSelector("div.post_header")); }
					catch (Exception ex)
					{
						try { File.WriteAllText(Path.ChangeExtension(fileImage, ".htm"), driver.PageSource); } catch { }
						try { driverSS.GetScreenshot().SaveAsFile(fileImage + ".jpg", ScreenshotImageFormat.Png); } catch { }
						throw new Exception("div.post_header" + "\t" + shortLink + "\t" + ex.Message);
					}
					IWebElement element2 = driver.FindElement(By.CssSelector("div.wall_text"));
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.ClassName("uiScaledImageContainer")).FindElement(By.XPath("../.."))); }
					catch { }
					width = Math.Max(element1.Size.Width, element2.Size.Width) - 30;
					height = element1.Size.Height + element2.Size.Height;
					height = height < driver.Manage().Window.Size.Height ? height : driver.Manage().Window.Size.Height - element1.Location.Y;
					x = element1.Location.X + 15;
					y = element1.Location.Y + 15;
				}
				else if (driverUrl.Contains("instagram.com"))
				{
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.XPath("//nav"))); } catch { }
					IWebElement elem = driver.FindElement(By.XPath("//article"));
					driverJS.ExecuteScript(jsScroll, elem);
					try
					{
						foreach (var elm in elem.FindElements(By.XPath("//time[@datetime]")))
							try
							{
								var pubDate = DateTime.ParseExact(elm.GetAttribute("datetime"), "yyyy-MM-ddTHH:mm:ss.000Z", CultureInfo.InvariantCulture)
									.ToLocalTime()
									.ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
								driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
								Console.WriteLine(pubDate);
							}
							catch { }
					}
					catch { }
					width = elem.Size.Width;
					height = elem.Size.Height;
					x = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.X;
					y = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.Y;
				}
				else if (driverUrl.Contains("livejournal.com"))
				{
					if (!driverUrl.Contains("?embed"))
					{
						driver.Navigate().GoToUrl(driverUrl + "?embed=1");
						while (String.IsNullOrWhiteSpace(driver.PageSource))
							Thread.Sleep(100);
					}
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.CssSelector("div.share-embed-footer__pane"))); } catch { }
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.CssSelector("div.share-embed-header__wrap-btn"))); } catch { }
					var element1 = driver.FindElement(By.XPath("//header"));
					var element2 = driver.FindElement(By.XPath("//article"));
					driverJS.ExecuteScript(jsScroll, element1);
					width = Math.Max(element1.Size.Width, element2.Size.Width);
					height = element1.Size.Height + element2.Size.Height;
					x = element1.Location.X;
					y = element1.Location.Y;
				}
				else if (driverUrl.Contains("twitter.com"))
				{
					Thread.Sleep(8000);
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.CssSelector("div.follow-bar"))); } catch { }
					try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.CssSelector("div.ProfileTweet-action--more"))); } catch { }
					IWebElement elem = null;
					try
					{
						elem = driver.FindElement(By.XPath("//div[@role='main' or contains(@class,'original-permalink-page')]"));
					}
					catch
					{
						try
						{
							elem = driver.FindElement(By.XPath("//div[contains(@class,'permalink-tweet')]"));
						}
						catch
						{
							try { elem = driver.FindElement(By.CssSelector("div.permalink-tweet-container")); }
							catch { elem = driver.FindElement(By.XPath(".//div[@data-testid='tweetDetail']")); }
						}
					}
					try { driverJS.ExecuteScript("arguments[0].style.display='none'", elem.FindElement(By.XPath(".//div[@aria-label='Tweet actions']"))); } catch { }
					try { driverJS.ExecuteScript("arguments[0].style.display='none'", elem.FindElement(By.XPath(".//div[text()='Likes']")).FindElement(By.XPath("../../../.."))); } catch { }
					try
					{
						foreach (var elm in elem.FindElements(By.XPath(".//div[contains(@class,'js-machine-translated-tweet-container') or contains(@class,'stream-item-footer') or contains(@class,'tweet-stats-container')]")))// or contains(@class,'stream-footer')
							driverJS.ExecuteScript(jsDrop, elm);
					}
					catch { }
					try
					{
						foreach (var elm in elem.FindElements(By.XPath("//span[contains(@class,'_timestamp')]")))
						{
							var ts = long.Parse(elm.GetAttribute("data-time"));
							var pubDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(ts).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
							driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
							Console.WriteLine(pubDate);
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
					try
					{
						foreach (var elm in elem.FindElements(By.XPath("//div[contains(@class,'ProfileTweet-action')]")))
							driverJS.ExecuteScript(jsHide, elm);
					}
					catch { }
					try
					{
						foreach (var elm in elem.FindElements(By.XPath("//div[@class='dropdown']")))
							driverJS.ExecuteScript(jsHide, elm);
					}
					catch { }
					try
					{
						foreach (var elm in elem.FindElements(By.XPath("//button[contains(@class,'Tombstone-action')]")))
							driverJS.ExecuteScript("arguments[0].click();", elm);
					}
					catch { }
					try
					{
						foreach (var elm in elem.FindElements(By.XPath(".//div[contains(@class,'tweet-stats-container') or @id='descendants']")))
							driverJS.ExecuteScript(jsDrop, elm);

					}
					catch { }
					try
					{
						foreach (var elm in elem.FindElements(By.XPath("//span[contains(@class,'_timestamp')]")))
						{
							var ts = long.Parse(elm.GetAttribute("data-time"));
							var pubDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(ts).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
							driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
							Console.WriteLine(pubDate);
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
					//driverJS.ExecuteScript(jsScroll, elem);
					//убираем рамку и скругленные края
					width = elem.Size.Width - 10;
					height = elem.Size.Height - 10;
					x = elem.Location.X + 5;
					y = elem.Location.Y + 5;
					//try
					//{
					//    var e = driver.FindElement(By.CssSelector("div.permalink-tweet-container"));
					//    driverJS.ExecuteScript(jsBorder, e);
					//    height = e.Size.Height - 10;
					//    x = elem.Location.X + 5;
					//    y = elem.Location.Y + 5;
					//}
					//catch (Exception ex)
					//{
					//    string s = ex.Message;
					//}
				}
				Directory.CreateDirectory(folder);
				try { driverSS.GetScreenshot().SaveAsFile(fileImage, ScreenshotImageFormat.Png); }
				catch { Thread.Sleep(2000); driverSS.GetScreenshot().SaveAsFile(fileImage, ScreenshotImageFormat.Png); }
			}
			finally
			{
				try { driver.Close(); } catch { }
				try { driver.Quit(); } catch { }
			}
			File.WriteAllBytes(fileImage, SaveAs(Crop(fileImage, width, height, x, y, shortLink), ImageFormat.Png, 100));
			return fileImage;
		}

		public static string Get(ChromeDriver driver, string shortLink, string fileImage)
		{
			if (File.Exists(fileImage) && new FileInfo(fileImage).Length > 0)
				return fileImage;
			var driverUrl = driver.Url;
			int width = 0, height = 0, x = 0, y = 0;
			try
			{
				int repeat = 300;
				while (!((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete") && repeat-- > 0)
					Thread.Sleep(100);
			}
			catch (Exception) { }
			try { driver.FindElement(By.TagName("body")).SendKeys(Keys.F11); }
			catch { }
			Thread.Sleep(8000);
			driverUrl = driver.Url.ToLower();
			if (driverUrl.Contains("livejournal.com"))
				try { driver.Manage().Window.Size = new Size(500, driver.Manage().Window.Size.Height); }
				catch { }
			else
				try { driver.Manage().Window.Maximize(); }
				catch { }
			if (driverUrl.Contains("facebook.com") && driverUrl.Contains("/videos/"))
			{
				driver.Navigate().GoToUrl(driverUrl.Replace("/videos/", "/posts/"));
				while (String.IsNullOrWhiteSpace(driver.PageSource))
					Thread.Sleep(100);
				driverUrl = driver.Url.ToLower();
			}
			var driverJS = (IJavaScriptExecutor)driver;
			var driverSS = (ITakesScreenshot)driver;
			var jsHide = "arguments[0].style.visibility='hidden'";
			var jsDrop = "arguments[0].style.display='none'";
			//var jsBorder = "arguments[0].style.border='solid 1px red'";
			var jsScroll = "arguments[0].scrollIntoView(true);";
			if (driverUrl.Contains("/t.me/"))
			{
				driver.Navigate().GoToUrl(driverUrl + "?embed=1");
				IWebElement elem = driver.FindElement(By.XPath("//div[@id='widget_message']"));
				driverJS.ExecuteScript(jsScroll, elem);
				width = elem.Size.Width;
				height = elem.Size.Height;
				x = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.X;
				y = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.Y;
			}
			else if (driverUrl.Contains("facebook.com"))
			{
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.XPath("//a[@aria-label='Story options']"))); } catch { }
				try { driverJS.ExecuteScript(jsDrop, driver.FindElement(By.Id("u_0_c"))); } catch { }
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.Id("headerArea"))); } catch { }
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.XPath("//a[text()='Follow']"))); } catch { }
				var elem = driver.FindElement(By.CssSelector("div.userContent")).FindElement(By.XPath(".."));
				driverJS.ExecuteScript(jsScroll, elem);
				width = elem.Size.Width;
				height = elem.Size.Height;
				x = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.X;
				y = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.Y;
			}
			else if (driverUrl.Contains("vk.com"))
			{
				try
				{
					foreach (var elm in driver.FindElements(By.XPath("//span[contains(@class,'rel_date_needs_update')]")))
					{
						var pubDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)
							.AddSeconds(long.Parse(elm.GetAttribute("time"))).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
						driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
				try
				{
					foreach (var elm in driver.FindElements(By.XPath("//*[contains(@class,'feature_tooltip__close')]")))
						driverJS.ExecuteScript("arguments[0].click();", elm);
				}
				catch { }
				try
				{
					foreach (var elm in driver.FindElements(By.XPath("//*[contains(@class,'feature_info_tooltip')]")))
						driverJS.ExecuteScript(jsHide, elm);
				}
				catch { }
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.ClassName("uiScaledImageContainer")).FindElement(By.XPath("../.."))); }
				catch { }

				IWebElement element1 = driver.FindElement(By.CssSelector("div.post_header"));
				IWebElement element2 = driver.FindElement(By.CssSelector("div.wall_text"));
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.ClassName("uiScaledImageContainer")).FindElement(By.XPath("../.."))); }
				catch { }
				width = Math.Max(element1.Size.Width, element2.Size.Width) - 30;
				height = element1.Size.Height + element2.Size.Height;
				height = height < driver.Manage().Window.Size.Height ? height : driver.Manage().Window.Size.Height - element1.Location.Y;
				x = element1.Location.X + 15;
				y = element1.Location.Y + 15;
			}
			else if (driverUrl.Contains("instagram.com"))
			{
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.XPath("//nav"))); } catch { }
				IWebElement elem = driver.FindElement(By.XPath("//article"));
				driverJS.ExecuteScript(jsScroll, elem);
				try
				{
					foreach (var elm in elem.FindElements(By.XPath("//time[@datetime]")))
						try
						{
							var pubDate = DateTime.ParseExact(elm.GetAttribute("datetime"), "yyyy-MM-ddTHH:mm:ss.000Z", CultureInfo.InvariantCulture)
								.ToLocalTime()
								.ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
							driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
							Console.WriteLine(pubDate);
						}
						catch { }
				}
				catch { }
				width = elem.Size.Width;
				height = elem.Size.Height;
				x = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.X;
				y = ((RemoteWebElement)elem).LocationOnScreenOnceScrolledIntoView.Y;
			}
			else if (driverUrl.Contains("livejournal.com"))
			{
				if (!driverUrl.Contains("?embed"))
				{
					driver.Navigate().GoToUrl(driverUrl + "?embed=1");
					while (String.IsNullOrWhiteSpace(driver.PageSource))
						Thread.Sleep(100);
				}
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.CssSelector("div.share-embed-footer__pane"))); } catch { }
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.CssSelector("div.share-embed-header__wrap-btn"))); } catch { }
				var element1 = driver.FindElement(By.XPath("//header"));
				var element2 = driver.FindElement(By.XPath("//article"));
				driverJS.ExecuteScript(jsScroll, element1);
				width = Math.Max(element1.Size.Width, element2.Size.Width);
				height = element1.Size.Height + element2.Size.Height;
				x = element1.Location.X;
				y = element1.Location.Y;
			}
			else if (driverUrl.Contains("twitter.com"))
			{
				Thread.Sleep(8000);
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.CssSelector("div.follow-bar"))); } catch { }
				try { driverJS.ExecuteScript(jsHide, driver.FindElement(By.CssSelector("div.ProfileTweet-action--more"))); } catch { }
				IWebElement elem = null;
				try
				{
					elem = driver.FindElement(By.XPath("//div[@role='main' or contains(@class,'original-permalink-page')]"));
				}
				catch
				{
					try
					{
						elem = driver.FindElement(By.XPath("//div[contains(@class,'permalink-tweet')]"));
					}
					catch
					{
						try { elem = driver.FindElement(By.CssSelector("div.permalink-tweet-container")); }
						catch { elem = driver.FindElement(By.XPath(".//div[@data-testid='tweetDetail']")); }
					}
				}
				try { driverJS.ExecuteScript("arguments[0].style.display='none'", elem.FindElement(By.XPath(".//div[@aria-label='Tweet actions']"))); } catch { }
				try { driverJS.ExecuteScript("arguments[0].style.display='none'", elem.FindElement(By.XPath(".//div[text()='Likes']")).FindElement(By.XPath("../../../.."))); } catch { }
				try
				{
					foreach (var elm in elem.FindElements(By.XPath(".//div[contains(@class,'js-machine-translated-tweet-container') or contains(@class,'stream-item-footer') or contains(@class,'tweet-stats-container')]")))// or contains(@class,'stream-footer')
						driverJS.ExecuteScript(jsDrop, elm);
				}
				catch { }
				try
				{
					foreach (var elm in elem.FindElements(By.XPath("//span[contains(@class,'_timestamp')]")))
					{
						var ts = long.Parse(elm.GetAttribute("data-time"));
						var pubDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(ts).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
						driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
						Console.WriteLine(pubDate);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
				try
				{
					foreach (var elm in elem.FindElements(By.XPath("//div[contains(@class,'ProfileTweet-action')]")))
						driverJS.ExecuteScript(jsHide, elm);
				}
				catch { }
				try
				{
					foreach (var elm in elem.FindElements(By.XPath("//div[@class='dropdown']")))
						driverJS.ExecuteScript(jsHide, elm);
				}
				catch { }
				try
				{
					foreach (var elm in elem.FindElements(By.XPath("//button[contains(@class,'Tombstone-action')]")))
						driverJS.ExecuteScript("arguments[0].click();", elm);
				}
				catch { }
				try
				{
					foreach (var elm in elem.FindElements(By.XPath(".//div[contains(@class,'tweet-stats-container') or @id='descendants']")))
						driverJS.ExecuteScript(jsDrop, elm);

				}
				catch { }
				try
				{
					foreach (var elm in elem.FindElements(By.XPath("//span[contains(@class,'_timestamp')]")))
					{
						var ts = long.Parse(elm.GetAttribute("data-time"));
						var pubDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(ts).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") + " (MSK)";
						driverJS.ExecuteScript("arguments[0].innerHTML='" + pubDate + "';", elm);
						Console.WriteLine(pubDate);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
				//driverJS.ExecuteScript(jsScroll, elem);
				//убираем рамку и скругленные края
				width = elem.Size.Width - 10;
				height = elem.Size.Height - 10;
				x = elem.Location.X + 5;
				y = elem.Location.Y + 5;
				//try
				//{
				//    var e = driver.FindElement(By.CssSelector("div.permalink-tweet-container"));
				//    driverJS.ExecuteScript(jsBorder, e);
				//    height = e.Size.Height - 10;
				//    x = elem.Location.X + 5;
				//    y = elem.Location.Y + 5;
				//}
				//catch (Exception ex)
				//{
				//    string s = ex.Message;
				//}
			}
			Directory.CreateDirectory(Path.GetDirectoryName(fileImage));
			try { driverSS.GetScreenshot().SaveAsFile(fileImage, ScreenshotImageFormat.Png); }
			catch
			{
				Thread.Sleep(2000);
				try { driverSS.GetScreenshot().SaveAsFile(fileImage, ScreenshotImageFormat.Png); }
				catch (Exception ex) { throw new Exception(fileImage + ", " + ex.Message); }
			}

			File.WriteAllBytes(fileImage, SaveAs(Crop(fileImage, width, height, x, y, shortLink), ImageFormat.Png, 100));
			return fileImage;
		}

		public static Image Crop(string fileImage, int width, int height, int x, int y, string link)
		{
			return Crop(File.ReadAllBytes(fileImage), width, height, x, y, link);
		}

		public static Image Crop(byte[] fileData, int width, int height, int x, int y, string link)
		{
			using (var ms = new MemoryStream(fileData))
			using (Image image = Image.FromStream(ms))
			{
				x = x >= 0 ? x : 0;
				y = y >= 0 ? y : 0;
				Console.WriteLine("width: {0}\theight: {1}", width, height);
				width = Math.Min(image.Width, width);
				height = height <= 0 ? image.Height : Math.Min(image.Height, height);
				Console.WriteLine("width: {0}\theight: {1}", width, height);
				if (width <= 0)
					width = 200;
				//try
				//{
				//	TrimWhitespace(image as Bitmap, ref width, ref height, ref x, ref y);
				//}
				//catch (Exception ex)
				//{
				//	Console.WriteLine(ex);
				//}
				var brushText = new SolidBrush(Color.White);
				var brushBack = new SolidBrush(Color.Silver);
				var dateText = link.Replace("https://", string.Empty).Replace("http://", string.Empty).Replace("www.", string.Empty) + " | t.me/SnapItBot";//, " + DateTime.Now.ToString("dd.MM.yy HH:mm") + "(MSK)
				float fontSize = 8;
				var font = new Font(new FontFamily("Lucida Console"), fontSize, FontStyle.Regular, GraphicsUnit.Point);
				Rectangle rectDest = new Rectangle(0, 0, width, height);
				Rectangle rectSrc = new Rectangle(x, y, width, height);
				Rectangle rectLabel;
				Image finalPic;
				SizeF textSize;
				try
				{
					using (Bitmap result = new Bitmap(width, height, image.PixelFormat))
					{
						result.SetResolution(image.HorizontalResolution, image.VerticalResolution);
						using (Graphics gfx = Graphics.FromImage(result))
						{
							textSize = gfx.MeasureString(dateText, font);
							gfx.FillRectangle(Brushes.White, 0, 0, result.Width, result.Height);
							gfx.SmoothingMode = SmoothingMode.None;
							gfx.CompositingQuality = CompositingQuality.HighQuality;
							gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
							gfx.PixelOffsetMode = PixelOffsetMode.HighQuality;
							gfx.DrawImage(image, rectDest, rectSrc, GraphicsUnit.Pixel);
						}
						finalPic = (Image)result.Clone();
					}
				}
				catch (Exception ex)
				{
					throw new Exception(ex.Message + "\r\n" + "width: " + width + ", height: " + height);
				}
				rectLabel = new Rectangle(
					finalPic.Width - (int)Math.Ceiling(textSize.Width),
					finalPic.Height - (int)Math.Ceiling(textSize.Height),
					(int)Math.Ceiling(textSize.Width),
					(int)Math.Ceiling(textSize.Height)
				);

				var marginHeight = Math.Max(20, (int)Math.Ceiling(textSize.Height));
				var marginWidth = Math.Max(20, (int)Math.Ceiling(textSize.Height));

				if (CountDiffColoredPixelsRectangle((Bitmap)image, rectLabel, Color.White) == 0)
					marginHeight = 0;

				using (finalPic)
				using (Bitmap result = new Bitmap(width + marginWidth, height + marginHeight, image.PixelFormat))
				{
					result.SetResolution(image.HorizontalResolution, image.VerticalResolution);
					using (Graphics gfx = Graphics.FromImage(result))
					{
						gfx.FillRectangle(Brushes.White, 0, 0, result.Width, result.Height);
						gfx.SmoothingMode = SmoothingMode.None;
						gfx.CompositingQuality = CompositingQuality.HighQuality;
						gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
						gfx.PixelOffsetMode = PixelOffsetMode.HighQuality;
						gfx.DrawImage(finalPic, marginWidth / 2, marginHeight / 2);
						var textX = finalPic.Width - (int)Math.Ceiling(textSize.Width) + marginWidth / 2;
						var textY = marginHeight == 0
							? finalPic.Height - (int)Math.Ceiling(textSize.Height)
							: finalPic.Height + marginHeight / 2;
						gfx.FillRectangle(brushBack, textX, textY, (int)Math.Ceiling(textSize.Width), (int)Math.Ceiling(textSize.Height));
						gfx.DrawString(dateText, font, brushText, textX, textY);
					}
					return (Image)result.Clone();
				}
			}
		}

		public static byte[] SaveAs(Image image, ImageFormat imageFormat, Int64 quality)
		{
			ImageCodecInfo[] imageCodecs = ImageCodecInfo.GetImageDecoders();
			ImageCodecInfo formatEncoder = Array.Find<ImageCodecInfo>(imageCodecs
				, (c => c.FormatID == imageFormat.Guid));
			if (formatEncoder == null)
				formatEncoder = Array.Find<ImageCodecInfo>(imageCodecs
					, (c => c.FormatID == ImageFormat.Jpeg.Guid));
			using (var qualityEncoderParameter = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality))
			using (var encoderParameters = new EncoderParameters(1))
			using (var ms = new MemoryStream())
			{
				encoderParameters.Param[0] = qualityEncoderParameter;
				image.Save(ms, formatEncoder, encoderParameters);
				return ms.ToArray();
			}
		}

		public static void TrimWhitespace(Bitmap main, ref int width, ref int height, ref int cropX, ref int cropY)
		{
			int? startX = null;
			int? startY = null;
			int? endX = null;
			int? endY = null;

			int widthFull = Math.Min(main.Width, width + cropX);
			int heightFull = Math.Min(main.Height, height + cropY);
			int stride;
			int bytesPerPixel;
			byte[] data;
			BitmapData bmMainData = main.LockBits(new Rectangle(0, 0, main.Width, main.Height), ImageLockMode.ReadOnly, main.PixelFormat);
			stride = bmMainData.Stride;
			bytesPerPixel = Math.Abs(stride) / main.Width;
			data = new byte[Math.Abs(stride) * main.Height];
			System.Runtime.InteropServices.Marshal.Copy(bmMainData.Scan0, data, 0, data.Length);
			main.UnlockBits(bmMainData);

			var mark = Color.White;
			var colorWhite = MyColor.FromARGB(mark.A, mark.R, mark.G, mark.B);

			var counter = 0;
			for (int x = cropX; x < widthFull && !startX.HasValue; ++x)
				if ((counter = CountDiffColoredPixelsColumn(data, stride, bytesPerPixel, x, cropY, heightFull, colorWhite)) > 0)
					startX = x;

			for (int y = cropY; y < heightFull && !startY.HasValue; ++y)
				if ((counter = CountDiffColoredPixelsRow(data, stride, bytesPerPixel, y, cropX, widthFull, colorWhite)) > 0)
					startY = y;

			for (int x = widthFull - 1; x > startX && !endX.HasValue; --x)
				if ((counter = CountDiffColoredPixelsColumn(data, stride, bytesPerPixel, x, cropY, heightFull, colorWhite)) > 0)
					endX = x;

			for (int y = heightFull - 1; y > startY && !endY.HasValue; --y)
				if ((counter = CountDiffColoredPixelsRow(data, stride, bytesPerPixel, y, cropX, widthFull, colorWhite)) > 0)
					endY = y + 5;

			cropX = startX.HasValue ? startX.Value : cropX;
			cropY = startY.HasValue ? startY.Value : cropY;
			width = endX.HasValue ? endX.Value - cropX : width;
			height = endY.HasValue ? endY.Value - cropY : height;

			//cropX = cropX - 20 > 0 ? cropX - 20 : cropX - 10 > 0 ? cropX - 10 : 0;
			//cropY = cropY - 20 > 0 ? cropY - 20 : cropY - 10 > 0 ? cropY - 10 : 0;
			//width = width + 10 < widthFull ? width + 10 : width;
			//height = height + 10 < heightFull ? height + 10 : height;
		}

		static int CountDiffColoredPixelsRow(byte[] data, int stride, int bytesPerPixel, int row, int beginX, int width, MyColor color)
		{
			//var colorGrey = ColorTranslator.FromHtml("#F5F8FA");
			//var colorUseAsWhite = MyColor.FromARGB(colorGrey.A, colorGrey.R, colorGrey.G, colorGrey.B);
			//var colorGrey1 = ColorTranslator.FromHtml("#E6ECF0");
			//var colorUseAsWhite1 = MyColor.FromARGB(colorGrey1.A, colorGrey1.R, colorGrey1.G, colorGrey1.B);
			int counter = 0;
			for (int x = beginX; x < width; ++x)
				try
				{
					if (!GetColor(x, row, stride, data, bytesPerPixel).Equals(color))
						counter++;
					//else if (GetColor(x, row, stride, data, bytesPerPixel).Equals(colorUseAsWhite))
					//	counter += 0;
					//else if (GetColor(x, row, stride, data, bytesPerPixel).Equals(colorUseAsWhite1))
					//	counter += 0;
					//else
					//	counter += 1;
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
			return counter;
		}

		static int CountDiffColoredPixelsColumn(byte[] data, int stride, int bytesPerPixel, int col, int beginY, int height, MyColor color)
		{
			//var colorGrey = ColorTranslator.FromHtml("#F5F8FA");
			//var colorUseAsWhite = MyColor.FromARGB(colorGrey.A, colorGrey.R, colorGrey.G, colorGrey.B);
			//var colorGrey1 = ColorTranslator.FromHtml("#E6ECF0");
			//var colorUseAsWhite1 = MyColor.FromARGB(colorGrey1.A, colorGrey1.R, colorGrey1.G, colorGrey1.B);
			int counter = 0;
			for (int y = beginY; y < height; ++y)
				try
				{
					if (GetColor(col, y, stride, data, bytesPerPixel).Equals(color))
						counter++;
					//if (GetColor(col, y, stride, data, bytesPerPixel).Equals(color))
					//	counter += 0;
					//else if (GetColor(col, y, stride, data, bytesPerPixel).Equals(colorUseAsWhite))
					//	counter += 0;
					//else if (GetColor(col, y, stride, data, bytesPerPixel).Equals(colorUseAsWhite1))
					//	counter += 0;
					//else
					//	counter += 1;
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex);
				}
			return counter;
		}

		static int CountDiffColoredPixelsRectangle(Bitmap main, Rectangle rectangle, Color color)
		{
			int stride;
			int bytesPerPixel;
			byte[] data;
			int width = main.Width;
			int height = main.Height;
			BitmapData bmMainData = main.LockBits(new Rectangle(0, 0, main.Width, main.Height), ImageLockMode.ReadOnly, main.PixelFormat);
			stride = bmMainData.Stride;
			bytesPerPixel = Math.Abs(stride) / main.Width;
			data = new byte[Math.Abs(stride) * main.Height];
			System.Runtime.InteropServices.Marshal.Copy(bmMainData.Scan0, data, 0, data.Length);
			main.UnlockBits(bmMainData);

			var colorCheck = MyColor.FromARGB(color.A, color.R, color.G, color.B);
			var colorGrey = ColorTranslator.FromHtml("#F5F8FA");
			var colorUseAsWhite = MyColor.FromARGB(colorGrey.A, colorGrey.R, colorGrey.G, colorGrey.B);
			var colorGrey1 = ColorTranslator.FromHtml("#E6ECF0");
			var colorUseAsWhite1 = MyColor.FromARGB(colorGrey1.A, colorGrey1.R, colorGrey1.G, colorGrey1.B);
			int counter = 0;
			for (int x = rectangle.X; x < Math.Min(width, rectangle.X + rectangle.Width); ++x)
				for (int y = rectangle.Y; y < Math.Min(height, rectangle.Y + rectangle.Height); ++y)
					try
					{
						if (GetColor(x, y, stride, data, bytesPerPixel).Equals(colorCheck))
							counter += 0;
						else if (GetColor(x, y, stride, data, bytesPerPixel).Equals(colorUseAsWhite))
							counter += 0;
						else if (GetColor(x, y, stride, data, bytesPerPixel).Equals(colorUseAsWhite1))
							counter += 0;
						else
							counter += 1;
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
			return counter;
		}

		public static SortedDictionary<string, int> CountColorDistribution(Bitmap main)
		{
			int stride;
			int bpp;
			byte[] data;
			int width = main.Width;
			int height = main.Height;
			BitmapData bmMainData = main.LockBits(new Rectangle(0, 0, main.Width, main.Height), ImageLockMode.ReadOnly, main.PixelFormat);
			stride = bmMainData.Stride;
			bpp = Math.Abs(stride) / main.Width;
			data = new byte[Math.Abs(stride) * main.Height];
			System.Runtime.InteropServices.Marshal.Copy(bmMainData.Scan0, data, 0, data.Length);
			main.UnlockBits(bmMainData);
			var retVal = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			for (int x = 0; x < width; ++x)
				for (int y = 0; y < height; ++y)
					try
					{
						var htmlColor = ColorTranslator.ToHtml(GetColorRGB(x, y, stride, data, bpp).ToColor());
						if (!retVal.ContainsKey(htmlColor))
							retVal.Add(htmlColor, 1);
						else
							retVal[htmlColor]++;
					}
					catch (Exception ex)
					{
						Console.WriteLine(ex);
					}
			return retVal;
		}


		static MyColor GetColorRGB(int x, int y, int stride, byte[] data, int bitCount)
		{
			int pos = y * stride + x * bitCount;
			byte r = data[pos + 2];
			byte g = data[pos + 1];
			byte b = data[pos + 0];
			return MyColor.FromARGB(0xff, r, g, b);
		}

		static MyColor GetColor(int x, int y, int stride, byte[] data, int bitCount)
		{
			int pos = y * stride + x * bitCount;
			byte a = data[pos + 3];
			byte r = data[pos + 2];
			byte g = data[pos + 1];
			byte b = data[pos + 0];
			return MyColor.FromARGB(a, r, g, b);
		}

		struct MyColor
		{
			byte A;
			byte R;
			byte G;
			byte B;

			public Color ToColor()
			{
				return Color.FromArgb(this.A, this.R, this.G, this.B);
			}

			public static MyColor FromARGB(byte a, byte r, byte g, byte b)
			{
				MyColor mc = new MyColor();
				mc.A = a;
				mc.R = r;
				mc.G = g;
				mc.B = b;
				return mc;
			}

			public override bool Equals(object obj)
			{
				if (!(obj is MyColor))
					return false;
				MyColor color = (MyColor)obj;
				if (color.A == this.A && color.R == this.R && color.G == this.G && color.B == this.B)
					return true;
				if (this.A == color.R && this.R == color.G && this.G == color.B && this.B == color.A)
					return true;
				return false;
			}

			public override int GetHashCode()
			{
				return base.GetHashCode();
			}

			public override string ToString()
			{
				return "0x" + A.ToString("X2") + R.ToString("X2") + G.ToString("X2") + B.ToString("X2");
			}
		}

		static MD5 hasher = MD5.Create();
		static readonly object hasherLock = new object[] { };
		private static string ComputeHash(string text)
		{
			return ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
		}

		private static string ComputeHash(byte[] textData)
		{
			byte[] hashData;
			lock (hasherLock)
			{
				try { hashData = hasher.ComputeHash(textData); }
				catch (CryptographicException)
				{
					if (hasher != null) hasher.Dispose();
					hasher = MD5.Create();
					hashData = hasher.ComputeHash(textData);
				}
			}
			return BitConverter.ToString(hashData).Replace("-", string.Empty);
		}
	}
}