namespace KashinChatBotService
{
    using ImageProcessor;
    using ImageProcessor.Plugins.WebP.Imaging.Formats;
    using NLog;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.Drawing.Text;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Telegram.Bot;
    using Telegram.Bot.Args;
    using Telegram.Bot.Types.InputFiles;
    using Topshelf;

    static class Program
    {
        static int Main()
        {
            return (int)HostFactory.Run(x =>
            {
                x.SetServiceName("KASHINBOT");
                x.SetDisplayName("KASHINBOT");
                x.SetDescription("Бот Кашин чата");
                x.RunAsLocalSystem();
                x.StartAutomatically();
                x.Service<Service>();
                x.EnableServiceRecovery(r => r.RestartService(1));
            });
        }
    }

    class Service : ServiceControl
    {
        static Logger Log = GetLogger();
        static readonly string ConfigBotKey = ConfigurationManager.AppSettings["bot_key"];
        static readonly string folderCurrent = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        static TelegramBotClient Bot = new TelegramBotClient(ConfigBotKey);
        static SortedSet<string> obscene = new SortedSet<string>("блад,бля,бляд,блят,говноед,ебал,ебат,ебись,збс,калоед,мудак,муден,пздц,пидор,пидр,пизд,пиздец,пиздос,пиздц,сука,хй,хуепут,хуи,хуисас,хуисос,хуй,ёб,ёба".Split(',')
            , StringComparer.OrdinalIgnoreCase);
        static readonly string lyrics = "Вот избран новый Президент\r\nСоединенных Штатов\r\nПоруган старый Президент\r\nСоединенных Штатов\r\n\r\nА нам-то что - ну, Президент\r\nНу, Съединенных Штатов\r\nА интересно все ж - Президент\r\nСоединенных Штатов";
        static readonly string[] videos = "DQACAgIAAxkDAAIEfl-hWGkIGlt9ox_aV5HCHoFzu6uLAAMJAAIKYQlJB2yyvqNHbAkeBA,DQACAgIAAxkDAAIEf1-hWG0NZKLp6QTGD-u-N0pB3C7dAAIBCQACCmEJSZZ3aBCNxEZiHgQ,DQACAgIAAxkDAAIEgF-hWG4yLT13doQC3ITCPosStLOOAAICCQACCmEJSTobDAOuB2-jHgQ,DQACAgIAAxkDAAIEgV-hWG4kS9Qd1On9gSa-oZ-ab3yXAAIDCQACCmEJSe51bd9iyaVEHgQ,DQACAgIAAxkDAAIEgl-hWG9D3WHMnhMTboOR6Ez_jd4XAAIECQACCmEJSX4HDOmNplicHgQ,DQACAgIAAxkDAAIEg1-hWHC4g6mPH7m1V6XwmEVCp1pAAAIFCQACCmEJSR_xnUupGljKHgQ".Split(',');
        public bool Start(HostControl hostControl)
        {
            Bot.SetWebhookAsync("").Wait(); // Обязательно! убираем старую привязку к вебхуку для бота
            Bot.OnUpdate += (object su, UpdateEventArgs evu) =>
            {
                try
                {
                    if (evu.Update.CallbackQuery != null || evu.Update.InlineQuery != null)
                        return; // в этом блоке нам келлбэки и инлайны не нужны
                    var message = evu.Update.Message;
                    if (message == null)
                        return;
                    if (message.Type == Telegram.Bot.Types.Enums.MessageType.ChatMembersAdded)
                        Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile("CAACAgIAAxUAAV-gU5wEIiOUCtqfKySQKAVJJ--yAAJLCwACI7jfCGg5X0t4-mLoHgQ"), replyToMessageId: message.MessageId).Wait();
                    else if (message.Type != Telegram.Bot.Types.Enums.MessageType.Text && message.Type != Telegram.Bot.Types.Enums.MessageType.Document)
                        return;
                    else
                    {
                        if (message.Text.Trim().StartsWith("/sticker"))
                        {
                            using (var ms = new MemoryStream())
                            {
                                using (var img = Image.FromFile(CreateSticker(message.Text.Trim().Substring("/sticker".Length).Trim())))
                                using (var imageFactory = new ImageFactory(preserveExifData: false))
                                    imageFactory.Load(img).Format(new WebPFormat()).Quality(100).Save(ms);
                                Bot.SendStickerAsync(message.Chat.Id, new InputOnlineFile(ms)).Wait();
                            }
                            return;
                        }
                        var slotUrl = Regex.Replace(message.Text.Trim(), @"^/[^\s]+", string.Empty).Replace("\r", string.Empty).ToLower().Trim();
                        if (slotUrl.Contains("трамп") || slotUrl.Contains("байден") || slotUrl.Contains("баиден"))
                            Bot.SendTextMessageAsync(message.Chat.Id, lyrics, replyToMessageId: message.MessageId).Wait();
                        else
                        {
                            var hasObscene = false;
                            foreach (var word in obscene)
                                if (slotUrl.Contains(word))
                                {
                                    hasObscene = true;
                                    break;
                                }
                            if (hasObscene)
                                Bot.SendVideoNoteAsync(message.Chat.Id, new InputOnlineFile(videos.OrderBy(s => Guid.NewGuid()).First()), replyToMessageId: message.MessageId).Wait();
                        }
                    }
                    return;
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            };
            // запускаем прием обновлений
            Bot.StartReceiving();
            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            try { Bot.SetWebhookAsync("").Wait(); }
            catch { }
            return true;
        }

        static string CreateSticker(string messageText)
        {
            var slotUrl = messageText;
            var foo = new PrivateFontCollection();
            foo.AddFontFile(Path.Combine(folderCurrent, "FreeSet-ExtraBoldOblique.otf"));
            var fontSize = 72f;
            var font = new Font((FontFamily)foo.Families[0], fontSize);
            SizeF labelMeasure;
            //SizeF labelMeasureStable;
            using (Bitmap result = new Bitmap(512, 512, PixelFormat.Format24bppRgb))
            {
                result.SetResolution(96f, 96f);
                using (Graphics graphics = Graphics.FromImage(result))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    font = new Font((FontFamily)foo.Families[0], fontSize);
                    labelMeasure = graphics.MeasureString(slotUrl, font);
                }
            }
            var backColor = ColorTranslator.FromHtml("#e0312c");// FE95A3
            var rowsCount = Regex.Split(slotUrl, @"\r?\n").Length;
            var angle = 10;
            var b = (int)Math.Abs(((90 - angle) * Math.Tan(angle))) * rowsCount;
            using (var result = new Bitmap((int)labelMeasure.Width + b * 2, (int)labelMeasure.Height + (rowsCount * 20), PixelFormat.Format24bppRgb))
            {
                result.MakeTransparent();
                result.SetResolution(96f, 96f);
                using (var graphics = Graphics.FromImage(result))
                {
                    var brushSolid = new SolidBrush(backColor);// new TextureBrush(img);// 
                    graphics.Clear(Color.Transparent);
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    graphics.FillPolygon(brushSolid, new[] { new Point(b, 0), new Point(b, result.Height), new Point(0, result.Height), new Point(b, 0) });
                    graphics.FillPolygon(brushSolid, new[] { new Point(result.Width - 1, 0), new Point(result.Width - b, result.Height), new Point(result.Width - b, 0), new Point(result.Width - 1, 0) });
                    graphics.FillRectangle(brushSolid, b, 0, result.Width - (b * 2), result.Height);
                    var offset = 10;
                    var barWidth = result.Width - b;
                    foreach (var line in slotUrl.Split('\n'))
                    {
                        var text = line;
                        var strikeout = false;
                        var leftAlign = false;
                        if (line.StartsWith(@"\s") || line.StartsWith(@"\l"))
                        {
                            strikeout = line.StartsWith(@"\s");
                            leftAlign = line.StartsWith(@"\l");
                            text = line.Substring(2);
                        }
                        var m = graphics.MeasureString(text, font);
                        var textLeft = b;
                        if (!leftAlign)
                            textLeft += (int)((barWidth - m.Width) - (barWidth - m.Width) / 2) - (rowsCount + 1) * 20;
                        graphics.DrawString(text, font, new SolidBrush(Color.White), new PointF(textLeft, offset));
                        if (strikeout)
                        {
                            var height = 25;
                            var width = 10;
                            var top = (int)(offset + m.Height / 2);// - height / 4);
                            var left = (int)(textLeft);
                            var right = (int)(m.Width + 20 + (barWidth - m.Width) - (barWidth - m.Width) / 2);
                            var brush = new SolidBrush(Color.White);
                            graphics.FillPolygon(brush, new[] { new Point(left + width, top), new Point(left + width, top + height), new Point(left, top + height), new Point(left + width, top) });
                            graphics.DrawLine(new Pen(brush, height), left + width, top + height / 2, right, top);
                            graphics.FillPolygon(brush, new[] { new Point(right, top - height / 2), new Point(right + width, top - height / 2), new Point(right, top + height - height / 2), new Point(right, top - height / 2) });
                        }
                        offset += (int)m.Height + 5;
                    }
                }

                using (var ms = new MemoryStream(ImageResize.Resize(result, null, 512, null, null, null, false)))
                using (var iii = Image.FromStream(ms))
                using (Bitmap r = new Bitmap(512, iii.Height, PixelFormat.Format24bppRgb))
                {
                    r.MakeTransparent();
                    r.SetResolution(96f, 96f);
                    using (Graphics graphics = Graphics.FromImage(r))
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                        graphics.DrawImage(iii, r.Width - iii.Width, 0, r.Width, r.Height);
                    }
                    var fileResult = Path.Combine(folderCurrent, DateTime.Now.Ticks.ToString() + ".png");
                    File.WriteAllBytes(fileResult, ImageResize.SaveAs(r, ImageFormat.Png, 100));
                    return fileResult;
                }
            }
        }

        static Logger GetLogger()
        {
            var logFileName = "NLog.config";
            if (!File.Exists(logFileName))
                File.WriteAllText(logFileName, @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<nlog xmlns=""http://www.nlog-project.org/schemas/NLog.xsd""
      xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
      xsi:schemaLocation=""http://www.nlog-project.org/schemas/NLog.xsd NLog.xsd""
      autoReload=""true"">
	<targets>
		<target	xsi:type=""File"" name=""file_log""
			fileName=""${basedir}/Logs/${shortdate}.log""
			layout=""${longdate} | ${uppercase:${level}} | ${windows-identity} | ${message}"" />
	</targets>
	<rules>
		<logger name=""*"" minlevel=""Error"" writeTo=""file_log"" />
		<!-- Trace,Debug,Info,Warn,Error,Fatal    -->
	</rules>
</nlog>");
            return LogManager.GetCurrentClassLogger();
        }
    }
}