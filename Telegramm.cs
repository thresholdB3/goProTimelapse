
using Telegram.Bot;

namespace GoProTimelapse
{
    public class Telegramm
    {
        public async Task SendVideo(string videoName, string botToken, int chatID)
        {
            using var cts = new CancellationTokenSource();
            var bot = new TelegramBotClient(botToken, cancellationToken: cts.Token);

            await using Stream stream = File.OpenRead($"./{videoName}");
            await bot.SendVideo(chatID, stream);
        }
    }
}
