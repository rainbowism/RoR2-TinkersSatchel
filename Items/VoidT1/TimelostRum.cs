﻿using RoR2;
using UnityEngine;
using System.Collections.ObjectModel;
using TILER2;
using static TILER2.MiscUtil;
using System.Collections.Generic;
using RoR2.Projectile;
using UnityEngine.AddressableAssets;
using RoR2.ExpansionManagement;
using System.Linq;

namespace ThinkInvisible.TinkersSatchel {
    public class TimelostRum : Item<TimelostRum> {

        ////// Item Data //////

        public override string displayName => "Timelost Rum";
        public override ItemTier itemTier => ItemTier.VoidTier1;
        public override ReadOnlyCollection<ItemTag> itemTags => new ReadOnlyCollection<ItemTag>(new[] {ItemTag.Damage});

        protected override string GetNameString(string langid = null) => displayName;
        protected override string GetPickupString(string langid = null) => "Chance to shoot temporal echoes of projectiles. <style=cIsVoid>Corrupts all Sturdy Mugs</style>.";
        protected override string GetDescString(string langid = null) => $"All projectile attacks gain a <style=cIsDamage>{Pct(procChance)} <style=cStack>(+{Pct(procChance)} per stack)</style></style> chance to fire <style=cIsDamage>an extra copy</style> with <color=#FF7F7F>{delayTime:N1} s of delay</style>. <style=cIsVoid>Corrupts all Sturdy Mugs</style>.";
        protected override string GetLoreString(string langid = null) => "What's left of the label reads: \"『?ROC??K」 and 【S?TO?NE』!\"";



        ////// Config //////

        [AutoConfigRoOSlider("{0:N1} s", 0f, 10f)]
        [AutoConfigUpdateActions(AutoConfigUpdateActionTypes.InvalidateLanguage)]
        [AutoConfig("Delay per extra projectile.", AutoConfigFlags.PreventNetMismatch, 0f, float.MaxValue)]
        public float delayTime { get; private set; } = 0.5f;

        [AutoConfigRoOSlider("{0:P0}", 0f, 1f)]
        [AutoConfigUpdateActions(AutoConfigUpdateActionTypes.InvalidateLanguage)]
        [AutoConfig("Extra projectile chance per item.", AutoConfigFlags.PreventNetMismatch, 0f, float.MaxValue)]
        public float procChance { get; private set; } = 0.1f;



        /////// Other Fields/Properties //////

        public bool ignoreMugs = false;
        public bool proccing = false;
        public List<(BulletAttack bi, float timestamp, float delay)> delayedBulletAttacks = new List<(BulletAttack, float, float)>();
        public List<(FireProjectileInfo fpi, float timestamp, float delay)> delayedProjectiles = new List<(FireProjectileInfo, float, float)>();


        ////// TILER2 Module Setup //////
        #region TILER2 Module Setup
        public TimelostRum() {
            modelResource = TinkersSatchelPlugin.resources.LoadAsset<GameObject>("Assets/TinkersSatchel/Prefabs/Items/TimelostRum.prefab");
            iconResource = TinkersSatchelPlugin.resources.LoadAsset<Sprite>("Assets/TinkersSatchel/Textures/ItemIcons/timelostRumIcon.png");
        }

        public override void SetupAttributes() {
            base.SetupAttributes();

            itemDef.requiredExpansion = Addressables.LoadAssetAsync<ExpansionDef>("RoR2/DLC1/Common/DLC1.asset")
                .WaitForCompletion();

            On.RoR2.ItemCatalog.SetItemRelationships += (orig, providers) => {
                var isp = ScriptableObject.CreateInstance<ItemRelationshipProvider>();
                isp.relationshipType = DLC1Content.ItemRelationshipTypes.ContagiousItem;
                isp.relationships = new[] {new ItemDef.Pair {
                    itemDef1 = Mug.instance.itemDef,
                    itemDef2 = itemDef
                }};
                orig(providers.Concat(new[] { isp }).ToArray());
            };
        }

        public override void SetupBehavior() {
            base.SetupBehavior();

            itemDef.unlockableDef = Mug.instance.unlockable;
        }

        public override void Install() {
            base.Install();

            //main tracking
            On.RoR2.Projectile.ProjectileManager.FireProjectile_FireProjectileInfo += ProjectileManager_FireProjectile_FireProjectileInfo;
            On.RoR2.BulletAttack.Fire += BulletAttack_Fire;
            On.RoR2.BulletAttack.FireSingle += BulletAttack_FireSingle;
            On.RoR2.Run.FixedUpdate += Run_FixedUpdate;

            //blacklist
            On.EntityStates.Huntress.ArrowRain.DoFireArrowRain += ArrowRain_DoFireArrowRain;
            On.EntityStates.AimThrowableBase.FireProjectile += AimThrowableBase_FireProjectile;
            On.EntityStates.Treebot.Weapon.FireMortar2.Fire += FireMortar2_Fire;
            On.RoR2.MissileUtils.FireMissile_Vector3_CharacterBody_ProcChainMask_GameObject_float_bool_GameObject_DamageColorIndex_Vector3_float_bool += MissileUtils_FireMissile_MyKingdomForAStruct;
            On.EntityStates.Treebot.TreebotFireFruitSeed.OnEnter += TreebotFireFruitSeed_OnEnter;
            On.EntityStates.Mage.Weapon.PrepWall.OnExit += PrepWall_OnExit;
            On.EntityStates.Treebot.Weapon.CreatePounder.OnExit += CreatePounder_OnExit;
            On.EntityStates.Treebot.Weapon.AimFlower.FireProjectile += AimFlower_FireProjectile;
            On.EntityStates.FireFlower2.OnEnter += FireFlower2_OnEnter;
            On.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
            On.EntityStates.LaserTurbine.FireMainBeamState.OnExit += FireMainBeamState_OnExit;
            On.EntityStates.Mage.Weapon.BaseThrowBombState.Fire += BaseThrowBombState_Fire;
            On.RoR2.GlobalEventManager.OnHitEnemy += GlobalEventManager_OnHitEnemy;
            On.RoR2.EquipmentSlot.FireGummyClone += EquipmentSlot_FireGummyClone;
        }

        public override void Uninstall() {
            base.Uninstall();

            On.RoR2.Projectile.ProjectileManager.FireProjectile_FireProjectileInfo -= ProjectileManager_FireProjectile_FireProjectileInfo;
            On.RoR2.BulletAttack.Fire -= BulletAttack_Fire;
            On.RoR2.BulletAttack.FireSingle -= BulletAttack_FireSingle;
            On.RoR2.Run.FixedUpdate -= Run_FixedUpdate;

            On.EntityStates.Huntress.ArrowRain.DoFireArrowRain -= ArrowRain_DoFireArrowRain;
            On.EntityStates.AimThrowableBase.FireProjectile -= AimThrowableBase_FireProjectile;
            On.EntityStates.Treebot.Weapon.FireMortar2.Fire -= FireMortar2_Fire;
            On.RoR2.MissileUtils.FireMissile_Vector3_CharacterBody_ProcChainMask_GameObject_float_bool_GameObject_DamageColorIndex_Vector3_float_bool -= MissileUtils_FireMissile_MyKingdomForAStruct;
            On.EntityStates.Treebot.TreebotFireFruitSeed.OnEnter -= TreebotFireFruitSeed_OnEnter;
            On.EntityStates.Mage.Weapon.PrepWall.OnExit -= PrepWall_OnExit;
            On.EntityStates.Treebot.Weapon.CreatePounder.OnExit -= CreatePounder_OnExit;
            On.EntityStates.Treebot.Weapon.AimFlower.FireProjectile -= AimFlower_FireProjectile;
            On.EntityStates.FireFlower2.OnEnter -= FireFlower2_OnEnter;
            On.RoR2.GlobalEventManager.OnCharacterDeath -= GlobalEventManager_OnCharacterDeath;
            On.EntityStates.LaserTurbine.FireMainBeamState.OnExit -= FireMainBeamState_OnExit;
            On.EntityStates.Mage.Weapon.BaseThrowBombState.Fire -= BaseThrowBombState_Fire;
            On.RoR2.GlobalEventManager.OnHitEnemy -= GlobalEventManager_OnHitEnemy;
            On.RoR2.EquipmentSlot.FireGummyClone -= EquipmentSlot_FireGummyClone;
        }
        #endregion



        ////// Hooks //////
        #region Hooks
        private void Run_FixedUpdate(On.RoR2.Run.orig_FixedUpdate orig, Run self) {
            orig(self);
            ignoreMugs = true;
            proccing = true;
            for(var i = delayedBulletAttacks.Count - 1; i >= 0; i--) {
                if(delayedBulletAttacks[i].bi == null) {
                    delayedBulletAttacks.RemoveAt(i);
                    continue;
                }

                if(Time.fixedTime - delayedBulletAttacks[i].timestamp > delayedBulletAttacks[i].delay) {
                    delayedBulletAttacks[i].bi.Fire();
                    delayedBulletAttacks.RemoveAt(i);
                }
            }
            for(var i = delayedProjectiles.Count - 1; i >= 0; i--) {
                if(Time.fixedTime - delayedProjectiles[i].timestamp > delayedProjectiles[i].delay) {
                    ProjectileManager.instance.FireProjectile(delayedProjectiles[i].fpi);
                    delayedProjectiles.RemoveAt(i);
                }
            }
            proccing = false;
            ignoreMugs = false;
        }

        private void BulletAttack_Fire(On.RoR2.BulletAttack.orig_Fire orig, BulletAttack self) {
            orig(self);
            if(ignoreMugs || !self.owner) return;
            var cpt = self.owner.GetComponent<CharacterBody>();
            if(!cpt) return;
            var count = GetCount(cpt);
            if(count <= 0) return;
            var totalChance = count * procChance;
            int procCount = (Util.CheckRoll(Wrap(totalChance * 100f, 0f, 100f), cpt.master) ? 1 : 0) + (int)Mathf.Floor(totalChance);
            for(var i = 1; i <= procCount; i++)
                delayedBulletAttacks.Add((self, Time.fixedTime, i * delayTime));
        }

        private void BulletAttack_FireSingle(On.RoR2.BulletAttack.orig_FireSingle orig, BulletAttack self, Vector3 normal, int muzzleIndex) {
            if(proccing)
                self.weapon = null; //force tracer effect to happen in worldspace. BulletAttack.Fire sets weapon to owner if null, even if you set it to null on purpose >:(
            orig(self, normal, muzzleIndex);
        }

        private void ProjectileManager_FireProjectile_FireProjectileInfo(On.RoR2.Projectile.ProjectileManager.orig_FireProjectile_FireProjectileInfo orig, ProjectileManager self, FireProjectileInfo fireProjectileInfo) {
            orig(self, fireProjectileInfo);
            if(ignoreMugs || !self || !fireProjectileInfo.owner || !fireProjectileInfo.projectilePrefab || fireProjectileInfo.projectilePrefab.GetComponent<Deployable>()) return;
            var cpt = fireProjectileInfo.owner.GetComponent<CharacterBody>();
            if(!cpt) return;
            var count = GetCount(cpt);
            if(count <= 0) return;
            var totalChance = count * procChance;
            int procCount = (Util.CheckRoll(Wrap(totalChance * 100f, 0f, 100f), cpt.master) ? 1 : 0) + (int)Mathf.Floor(totalChance);
            for(var i = 1; i <= procCount; i++)
                delayedProjectiles.Add((fireProjectileInfo, Time.fixedTime, i * delayTime));
        }

        private void GlobalEventManager_OnHitEnemy(On.RoR2.GlobalEventManager.orig_OnHitEnemy orig, GlobalEventManager self, DamageInfo damageInfo, GameObject victim) {
            ignoreMugs = true;
            orig(self, damageInfo, victim);
            ignoreMugs = false;
        }

        private void BaseThrowBombState_Fire(On.EntityStates.Mage.Weapon.BaseThrowBombState.orig_Fire orig, EntityStates.Mage.Weapon.BaseThrowBombState self) {
            var doIgnore = self is EntityStates.GlobalSkills.LunarNeedle.ThrowLunarSecondary || self is EntityStates.Mage.Weapon.ThrowIcebomb;
            if(doIgnore) ignoreMugs = true;
            orig(self);
            if(doIgnore) ignoreMugs = false;
        }

        private void FireMainBeamState_OnExit(On.EntityStates.LaserTurbine.FireMainBeamState.orig_OnExit orig, EntityStates.LaserTurbine.FireMainBeamState self) {
            ignoreMugs = true;
            orig(self);
            ignoreMugs = false;
        }

        private void GlobalEventManager_OnCharacterDeath(On.RoR2.GlobalEventManager.orig_OnCharacterDeath orig, GlobalEventManager self, DamageReport damageReport) {
            ignoreMugs = true;
            orig(self, damageReport);
            ignoreMugs = false;
        }

        private void FireFlower2_OnEnter(On.EntityStates.FireFlower2.orig_OnEnter orig, EntityStates.FireFlower2 self) {
            ignoreMugs = true;
            orig(self);
            ignoreMugs = false;
        }

        private void AimFlower_FireProjectile(On.EntityStates.Treebot.Weapon.AimFlower.orig_FireProjectile orig, EntityStates.Treebot.Weapon.AimFlower self) {
            ignoreMugs = true;
            orig(self);
            ignoreMugs = false;
        }

        private void TreebotFireFruitSeed_OnEnter(On.EntityStates.Treebot.TreebotFireFruitSeed.orig_OnEnter orig, EntityStates.Treebot.TreebotFireFruitSeed self) {
            ignoreMugs = true;
            orig(self);
            ignoreMugs = false;
        }

        private void PrepWall_OnExit(On.EntityStates.Mage.Weapon.PrepWall.orig_OnExit orig, EntityStates.Mage.Weapon.PrepWall self) {
            ignoreMugs = true;
            orig(self);
            ignoreMugs = false;
        }

        private void CreatePounder_OnExit(On.EntityStates.Treebot.Weapon.CreatePounder.orig_OnExit orig, EntityStates.Treebot.Weapon.CreatePounder self) {
            ignoreMugs = true;
            orig(self);
            ignoreMugs = false;
        }

        private void ArrowRain_DoFireArrowRain(On.EntityStates.Huntress.ArrowRain.orig_DoFireArrowRain orig, EntityStates.Huntress.ArrowRain self) {
            ignoreMugs = true;
            orig(self);
            ignoreMugs = false;
        }

        private void MissileUtils_FireMissile_MyKingdomForAStruct(On.RoR2.MissileUtils.orig_FireMissile_Vector3_CharacterBody_ProcChainMask_GameObject_float_bool_GameObject_DamageColorIndex_Vector3_float_bool orig, Vector3 position, CharacterBody attackerBody, ProcChainMask procChainMask, GameObject victim, float missileDamage, bool isCrit, GameObject projectilePrefab, DamageColorIndex damageColorIndex, Vector3 initialDirection, float force, bool addMissileProc) {
            ignoreMugs = true;
            orig(position, attackerBody, procChainMask, victim, missileDamage, isCrit, projectilePrefab, damageColorIndex, initialDirection, force, addMissileProc);
            ignoreMugs = false;
        }

        private void AimThrowableBase_FireProjectile(On.EntityStates.AimThrowableBase.orig_FireProjectile orig, EntityStates.AimThrowableBase self) {
            var doIgnore = self is EntityStates.Treebot.Weapon.AimMortar2 || self is EntityStates.Captain.Weapon.CallAirstrikeBase;
            if(doIgnore) ignoreMugs = true;
            orig(self);
            if(doIgnore) ignoreMugs = false;
        }

        private void FireMortar2_Fire(On.EntityStates.Treebot.Weapon.FireMortar2.orig_Fire orig, EntityStates.Treebot.Weapon.FireMortar2 self) {
            ignoreMugs = true;
            orig(self);
            ignoreMugs = false;
        }

        private bool EquipmentSlot_FireGummyClone(On.RoR2.EquipmentSlot.orig_FireGummyClone orig, EquipmentSlot self) {
            ignoreMugs = true;
            var retv = orig(self);
            ignoreMugs = false;
            return retv;
        }
        #endregion
    }
}