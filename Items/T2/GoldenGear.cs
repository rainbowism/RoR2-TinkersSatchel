﻿using RoR2;
using UnityEngine;
using System.Collections.ObjectModel;
using TILER2;
using static TILER2.MiscUtil;
using static R2API.RecalculateStatsAPI;
using R2API;
using UnityEngine.AddressableAssets;

namespace ThinkInvisible.TinkersSatchel {
    public class GoldenGear : Item<GoldenGear> {

        ////// Item Data //////
        
        public override string displayName => "Armor Crystal";
        public override ItemTier itemTier => ItemTier.Tier2;
        public override ReadOnlyCollection<ItemTag> itemTags => new ReadOnlyCollection<ItemTag>(new[] {ItemTag.Healing});

        protected override string GetNameString(string langid = null) => displayName;
        protected override string GetPickupString(string langid = null) => "Gain armor by hoarding money.";
        protected override string GetDescString(string langid = null) => $"Gain <style=cIsHealing>armor</style> based on your currently held <style=cIsUtility>money</style>. The first point of <style=cIsHealing>armor</style> costs <style=cIsUtility>${goldAmt:N0}</style> <style=cStack>(-{Pct(goldReduc)} per stack, exponential; scales with difficulty)</style>; each subsequent point <style=cIsUtility>costs {Pct(goldExp)} more</style> than the last.";
        protected override string GetLoreString(string langid = null) => "";



        ////// Config //////

        [AutoConfigRoOIntSlider("${0:N0}", 1, 1000)]
        [AutoConfigUpdateActions(AutoConfigUpdateActionTypes.InvalidateLanguage | AutoConfigUpdateActionTypes.InvalidateStats)]
        [AutoConfig("Gold required for the first point of armor. Scales with difficulty level.", AutoConfigFlags.PreventNetMismatch, 1, int.MaxValue)]
        public int goldAmt { get; private set; } = 10;

        [AutoConfigRoOSlider("{0:P1}", 0f, 1f)]
        [AutoConfigUpdateActions(AutoConfigUpdateActionTypes.InvalidateLanguage | AutoConfigUpdateActionTypes.InvalidateStats)]
        [AutoConfig("Exponential factor of GoldAmt scaling per additional point of armor.", AutoConfigFlags.PreventNetMismatch, 0f, float.MaxValue)]
        public float goldExp { get; private set; } = 0.075f;

        [AutoConfigRoOSlider("{0:P1}", 0f, 0.999f)]
        [AutoConfigUpdateActions(AutoConfigUpdateActionTypes.InvalidateLanguage | AutoConfigUpdateActionTypes.InvalidateStats)]
        [AutoConfig("Inverse-exponential multiplier for reduced GoldAmt per stack (higher = more powerful).", AutoConfigFlags.PreventNetMismatch, 0f, 0.999f)]
        public float goldReduc { get; private set; } = 0.1f;

        [AutoConfigRoOSlider("{0:N4}", float.Epsilon, 1f)]
        [AutoConfigUpdateActions(AutoConfigUpdateActionTypes.InvalidateStats)]
        [AutoConfig("Minimum possible goldAmt as affected by item stacking.", AutoConfigFlags.PreventNetMismatch, float.Epsilon, float.MaxValue)]
        public float goldMin { get; private set; } = 0.0001f;

        [AutoConfigRoOCheckbox()]
        [AutoConfigUpdateActions(AutoConfigUpdateActionTypes.InvalidateStats)]
        [AutoConfig("If true, deployables (e.g. Engineer turrets) with Armor Crystal will benefit from their master's money.",
            AutoConfigFlags.PreventNetMismatch)]
        public bool inclDeploys { get; private set; } = true;



        ////// Other Fields/Properties //////
        
        public BuffDef goldenGearBuff { get; private set; }



        ////// TILER2 Module Setup //////
        
        public GoldenGear() {
            modelResource = TinkersSatchelPlugin.resources.LoadAsset<GameObject>("Assets/TinkersSatchel/Prefabs/Items/GoldenGear.prefab");
            iconResource = TinkersSatchelPlugin.resources.LoadAsset<Sprite>("Assets/TinkersSatchel/Textures/ItemIcons/goldenGearIcon.png");
        }

        public override void SetupAttributes() {
            base.SetupAttributes();

            goldenGearBuff = ScriptableObject.CreateInstance<BuffDef>();
            goldenGearBuff.buffColor = new Color(0.85f, 0.8f, 0.3f);
            goldenGearBuff.canStack = true;
            goldenGearBuff.isDebuff = false;
            goldenGearBuff.name = "TKSATGoldenGear";
            goldenGearBuff.iconSprite = Addressables.LoadAssetAsync<Sprite>("RoR2/Base/Common/texBuffGenericShield.tif")
                .WaitForCompletion();
            ContentAddition.AddBuffDef(goldenGearBuff);
        }

        public override void Install() {
            base.Install();

            On.RoR2.CharacterBody.FixedUpdate += On_CBFixedUpdate;
            GetStatCoefficients += Evt_TILER2GetStatCoefficients;
        }

        public override void Uninstall() {
            base.Uninstall();

            On.RoR2.CharacterBody.FixedUpdate -= On_CBFixedUpdate;
            GetStatCoefficients -= Evt_TILER2GetStatCoefficients;
        }



        ////// Hooks //////
        
        private void On_CBFixedUpdate(On.RoR2.CharacterBody.orig_FixedUpdate orig, CharacterBody self) {
            orig(self);
            UpdateGGBuff(self);
        }

        private void Evt_TILER2GetStatCoefficients(CharacterBody sender, StatHookEventArgs args) {
            if(!sender) return;
            var cpt = sender.GetComponent<GoldenGearComponent>();
            if(cpt) args.armorAdd += cpt.calculatedArmorBonus;
        }



        ////// Public Methods //////

        public float CalculateArmor(uint money, int stacks) {
            if(money == 0 || stacks <= 0) return 0;
            var baseCost = Mathf.Max(Run.instance.GetDifficultyScaledCost(goldAmt) * Mathf.Pow(1f - goldReduc, stacks - 1f),
                goldMin);
            return Mathf.Log(money * goldExp / baseCost + 1f) / Mathf.Log(goldExp + 1f);
        }



        ////// Non-Public Methods //////

        void UpdateGGBuff(CharacterBody cb) {
            if(!cb) return;

            var cpt = cb.GetComponent<GoldenGearComponent>();
            if(!cpt) cpt = cb.gameObject.AddComponent<GoldenGearComponent>();

            uint newMoney = 0;
            if(cb.master)
                newMoney = cb.master.money;
            if(inclDeploys) {
                var dplc = cb.GetComponent<Deployable>();
                if(dplc && dplc.ownerMaster) newMoney += dplc.ownerMaster.money;
            }
            var newDiff = Run.instance.difficultyCoefficient;
            var newIcnt = GetCount(cb);

            bool didChange = false;
            if(cpt.cachedMoney != newMoney) {
                didChange = true;
                cpt.cachedMoney = newMoney;
            }
            if(cpt.cachedDiff != newDiff) {
                didChange = true;
                cpt.cachedDiff = newDiff;
            }
            if(cpt.cachedIcnt != newIcnt) {
                didChange = true;
                cpt.cachedIcnt = newIcnt;
            }
            if(!didChange) return;

            cpt.calculatedArmorBonus = CalculateArmor(cpt.cachedMoney, cpt.cachedIcnt);

            var tgtBuffStacks = Mathf.FloorToInt(cpt.calculatedArmorBonus);

            int currBuffStacks = cb.GetBuffCount(goldenGearBuff);
            if(tgtBuffStacks != currBuffStacks)
                cb.SetBuffCount(goldenGearBuff.buffIndex, tgtBuffStacks);
        }
    }

    public class GoldenGearComponent : MonoBehaviour {
        public uint cachedMoney = 0u;
        public int cachedIcnt = 0;
        public float cachedDiff = 0f;
        public float calculatedArmorBonus = 0;
    }
}