using ArchiSteamFarm.Core;
using ArchiSteamFarm.Localization;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam.Interaction;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Web.Responses;
using SteamKit2;
using System.ComponentModel;
using System.Composition;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;
using static ArchiSteamFarm.Steam.Integration.ArchiWebHandler;

namespace GiftCodeRedeemer;

[Export(typeof(IPlugin))]
internal sealed partial class GiftCodeRedeemerPlugin : IBotCommand2, IGitHubPluginUpdates {

	public string Name => nameof(GiftCodeRedeemerPlugin);
	public string RepositoryName => "dm1tz/GiftCodeRedeemer";
	public Version Version => typeof(GiftCodeRedeemerPlugin).Assembly.GetName().Version ?? throw new InvalidOperationException(nameof(Version));

	[GeneratedRegex(@"^[0-9A-F]{16}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
	private static partial Regex GiftCodeRegex();

	public Task OnLoaded() => Task.CompletedTask;

	public async Task<string?> OnBotCommand(Bot bot, EAccess access, string message, string[] args, ulong steamID = 0) {
		ArgumentNullException.ThrowIfNull(bot);

		if (!Enum.IsDefined(access)) {
			throw new InvalidEnumArgumentException(nameof(access), (int) access, typeof(EAccess));
		}

		ArgumentException.ThrowIfNullOrEmpty(message);

		if ((args == null) || (args.Length == 0)) {
			throw new ArgumentNullException(nameof(args));
		}

		if ((steamID != 0) && !new SteamID(steamID).IsIndividualAccount) {
			throw new ArgumentOutOfRangeException(nameof(steamID));
		}

		return args[0].ToUpperInvariant() switch {
			"REDEEMGIFTCODE" or "RGC" when args.Length == 4 => await ResponseRedeemGiftCode(access, args[1], args[2], args[3], steamID).ConfigureAwait(false),
			"REDEEMGIFTCODE" or "RGC" when args.Length == 3 => await ResponseRedeemGiftCode(bot, access, args[1], args[2]).ConfigureAwait(false),
			"GCRVERSION" or "GCRV" => ResponseVersion(access),
			_ => null
		};
	}

	private static async Task<string?> ResponseRedeemGiftCode(Bot bot, EAccess access, string giftCode, string redeemer) {
		if (access < EAccess.Master) {
			return access > EAccess.None ? bot.Commands.FormatBotResponse(Strings.ErrorAccessDenied) : null;
		}

		if (!bot.IsConnectedAndLoggedOn) {
			return bot.Commands.FormatBotResponse(Strings.BotNotConnected);
		}

		if (!GiftCodeRegex().IsMatch(giftCode)) {
			return bot.Commands.FormatBotResponse(string.Format(CultureInfo.CurrentCulture, Strings.ErrorIsInvalid, nameof(giftCode)));
		}

		Uri request = new(SteamStoreURL, $"/account/ackgift/{giftCode}?redeemer={Uri.EscapeDataString(redeemer)}");

		using HtmlDocumentResponse? response = await bot.ArchiWebHandler.UrlGetToHtmlDocumentWithSession(request).ConfigureAwait(false);

		string? content = response?.Content?.DocumentElement?.OuterHtml;

		if (string.IsNullOrEmpty(content)) {
			return bot.Commands.FormatBotResponse(Strings.WarningFailed);
		}

		return bot.Commands.FormatBotResponse(Strings.Success);
	}

	private static async Task<string?> ResponseRedeemGiftCode(EAccess access, string botName, string giftCode, string redeemer, ulong steamID = 0) {
		ArgumentException.ThrowIfNullOrEmpty(botName);
		ArgumentException.ThrowIfNullOrEmpty(giftCode);
		ArgumentException.ThrowIfNullOrEmpty(redeemer);

		Bot? bot = Bot.GetBot(botName);

		if (bot == null) {
			return access >= EAccess.Master ? Commands.FormatStaticResponse(string.Format(CultureInfo.CurrentCulture, Strings.BotNotFound, botName)) : null;
		}

		return await ResponseRedeemGiftCode(bot, Commands.GetProxyAccess(bot, access, steamID), giftCode, redeemer).ConfigureAwait(false);
	}

	private static string? ResponseVersion(EAccess access) {
		if (access < EAccess.FamilySharing) {
			return access > EAccess.None ? Commands.FormatStaticResponse(Strings.ErrorAccessDenied) : null;
		}

		return Commands.FormatStaticResponse(string.Format(CultureInfo.CurrentCulture, Strings.BotVersion, nameof(GiftCodeRedeemerPlugin), typeof(GiftCodeRedeemerPlugin).Assembly.GetName().Version));
	}
}
