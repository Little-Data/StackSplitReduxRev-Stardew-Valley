using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using System;

namespace StackSplitRedux
    {
    /// <summary>GMCM 1.16+ API with i18n support via Func&lt;string&gt;.</summary>
    public interface IGenericModConfigMenuApi
        {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string> tooltip = null, int? min = null, int? max = null, int? interval = null, string fieldId = null);
        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string> tooltip = null, string[] allowedValues = null, Func<string, string> formatAllowedValue = null, string fieldId = null);
        void AddKeybind(IManifest mod, Func<SButton> getValue, Action<SButton> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddKeybindList(IManifest mod, Func<KeybindList> getValue, Action<KeybindList> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        }

    class ModConfigMenu
        {
        private readonly IManifest ModManifest;
        private readonly IModHelper Helper;

        internal ModConfigMenu() {
            this.Helper = Mod.Instance.Helper;
            this.ModManifest = Mod.Instance.ModManifest;
            Register();
            }

        void Register() {
            var api = Mod.Registry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (api is null) {
                Log.Trace("GMCM not available, skipping Mod Config Menu");
                return;
                }
            Log.TraceIfD($"[{nameof(ModConfigMenu)}] Registering {this.ModManifest.UniqueID} with GMCM");
            api.Register(
                this.ModManifest,
                () => {
                    Mod.Config = new ModConfig();
                    Log.TraceIfD($"[{nameof(ModConfigMenu)}] Config reset to defaults");
                    },
                () => {
                    try {
                        if (Mod.Config is not null) {
                            Log.TraceIfD(
                                $"[{nameof(ModConfigMenu)}] Saving config: DefaultCraftingAmount={Mod.Config.DefaultCraftingAmount}, " +
                                $"DefaultShopAmount={Mod.Config.DefaultShopAmount}, ModifierKey={Mod.Config.ModifierKey}, " +
                                $"DebuggingMode={Mod.Config.DebuggingMode}, EnableGenericMenuSupport={Mod.Config.EnableGenericMenuSupport}, " +
                                $"ShowCannotSplitHint={Mod.Config.ShowCannotSplitHint}, HintDurationSeconds={Mod.Config.HintDurationSeconds}, " +
                                $"HintOffsetX={Mod.Config.HintOffsetX}, HintOffsetY={Mod.Config.HintOffsetY}, " +
                                $"HintTextScalePercent={Mod.Config.HintTextScalePercent}, HintOutlineThickness={Mod.Config.HintOutlineThickness}, " +
                                $"HintShadowStrength={Mod.Config.HintShadowStrength}, HintTextColor=\"{Mod.Config.HintTextColor}\", " +
                                $"CannotSplitMessage=\"{Mod.Config.CannotSplitMessage}\""
                                );
                            }
                        }
                    catch {
                        // Logging must never break saving the config.
                        }
                    this.Helper.WriteConfig<ModConfig>(Mod.Config);
                    }
                );

            api.AddNumberOption(
                this.ModManifest,
                () => Mod.Config.DefaultCraftingAmount,
                (int value) => Mod.Config.DefaultCraftingAmount = value,
                () => Mod.I18n.Get("gmcm.default-crafting-amount.name"),
                () => Mod.I18n.Get("gmcm.default-crafting-amount.desc")
                );

            api.AddNumberOption(
                this.ModManifest,
                () => Mod.Config.DefaultShopAmount,
                (int value) => Mod.Config.DefaultShopAmount = value,
                () => Mod.I18n.Get("gmcm.default-shop-amount.name"),
                () => Mod.I18n.Get("gmcm.default-shop-amount.desc")
                );

            api.AddKeybindList(
                this.ModManifest,
                () => Mod.Config.ModifierKey,
                (KeybindList value) => Mod.Config.ModifierKey = value,
                () => Mod.I18n.Get("gmcm.modifier-key.name"),
                () => Mod.I18n.Get("gmcm.modifier-key.desc")
                );

            api.AddBoolOption(
                this.ModManifest,
                () => Mod.Config.DebuggingMode,
                (bool value) => Mod.Config.DebuggingMode = value,
                () => Mod.I18n.Get("gmcm.debug-mode.name"),
                () => Mod.I18n.Get("gmcm.debug-mode.desc")
                );

            api.AddBoolOption(
                this.ModManifest,
                () => Mod.Config.EnableGenericMenuSupport,
                (bool value) => Mod.Config.EnableGenericMenuSupport = value,
                () => Mod.I18n.Get("gmcm.generic-menus.name"),
                () => Mod.I18n.Get("gmcm.generic-menus.desc")
                );

            api.AddBoolOption(
                this.ModManifest,
                () => Mod.Config.ShowCannotSplitHint,
                (bool value) => Mod.Config.ShowCannotSplitHint = value,
                () => Mod.I18n.Get("gmcm.show-cannot-split.name"),
                () => Mod.I18n.Get("gmcm.show-cannot-split.desc")
                );

            api.AddTextOption(
                this.ModManifest,
                () => string.IsNullOrWhiteSpace(Mod.Config.CannotSplitMessage)
                    ? Mod.I18n.Get("hint.cannot-split").ToString()
                    : Mod.Config.CannotSplitMessage,
                (string value) => Mod.Config.CannotSplitMessage = value ?? "",
                () => Mod.I18n.Get("gmcm.cannot-split-message.name"),
                () => Mod.I18n.Get("gmcm.cannot-split-message.desc")
                );

            api.AddNumberOption(
                this.ModManifest,
                () => Mod.Config.HintDurationSeconds,
                (int value) => Mod.Config.HintDurationSeconds = value,
                () => Mod.I18n.Get("gmcm.hint-duration.name"),
                () => Mod.I18n.Get("gmcm.hint-duration.desc"),
                1, 10
                );

            api.AddNumberOption(
                this.ModManifest,
                () => Mod.Config.HintOffsetX,
                (int value) => Mod.Config.HintOffsetX = value,
                () => Mod.I18n.Get("gmcm.hint-offset-x.name"),
                () => Mod.I18n.Get("gmcm.hint-offset-x.desc"),
                -600, 600
                );

            api.AddNumberOption(
                this.ModManifest,
                () => Mod.Config.HintOffsetY,
                (int value) => Mod.Config.HintOffsetY = value,
                () => Mod.I18n.Get("gmcm.hint-offset-y.name"),
                () => Mod.I18n.Get("gmcm.hint-offset-y.desc"),
                -600, 600
                );

            api.AddNumberOption(
                this.ModManifest,
                () => Mod.Config.HintTextScalePercent,
                (int value) => Mod.Config.HintTextScalePercent = value,
                () => Mod.I18n.Get("gmcm.hint-text-scale.name"),
                () => Mod.I18n.Get("gmcm.hint-text-scale.desc"),
                50, 400, 10
                );

            api.AddNumberOption(
                this.ModManifest,
                () => Mod.Config.HintOutlineThickness,
                (int value) => Mod.Config.HintOutlineThickness = value,
                () => Mod.I18n.Get("gmcm.hint-outline.name"),
                () => Mod.I18n.Get("gmcm.hint-outline.desc"),
                0, 8
                );

            api.AddNumberOption(
                this.ModManifest,
                () => Mod.Config.HintShadowStrength,
                (int value) => Mod.Config.HintShadowStrength = value,
                () => Mod.I18n.Get("gmcm.hint-shadow.name"),
                () => Mod.I18n.Get("gmcm.hint-shadow.desc"),
                0, 100, 5
                );

            api.AddTextOption(
                this.ModManifest,
                () => Mod.Config.HintTextColor,
                (string value) => Mod.Config.HintTextColor = value ?? "",
                () => Mod.I18n.Get("gmcm.hint-color.name"),
                () => Mod.I18n.Get("gmcm.hint-color.desc")
                );

            api.AddNumberOption(
                this.ModManifest,
                () => Mod.Config.InputBoxOffsetX,
                (int value) => Mod.Config.InputBoxOffsetX = value,
                () => Mod.I18n.Get("gmcm.offset-x.name"),
                () => Mod.I18n.Get("gmcm.offset-x.desc")
                );

            api.AddNumberOption(
                this.ModManifest,
                () => Mod.Config.InputBoxOffsetY,
                (int value) => Mod.Config.InputBoxOffsetY = value,
                () => Mod.I18n.Get("gmcm.offset-y.name"),
                () => Mod.I18n.Get("gmcm.offset-y.desc")
                );
            }
        }
    }
