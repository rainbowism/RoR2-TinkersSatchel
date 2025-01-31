﻿using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using TILER2;
using UnityEngine;

namespace ThinkInvisible.TinkersSatchel {
    public class Danger : Artifact<Danger> {

        ////// Artifact Data //////
        
        public override string displayName => "Artifact of Danger";

        protected override string GetNameString(string langid = null) => displayName;
        protected override string GetDescString(string langid = null) => "Players can be killed in one hit.";



        ////// Config //////

        [AutoConfig("If true, disabling this artifact will prevent curses (max HP reduction) from removing OHP.",
            AutoConfigFlags.PreventNetMismatch)]
        public bool preventCurseWhileOff { get; private set; } = false;



        ////// TILER2 Module Setup //////
        
        public Danger() {
            iconResource = TinkersSatchelPlugin.resources.LoadAsset<Sprite>("Assets/TinkersSatchel/Textures/ArtifactIcons/danger_on.png");
            iconResourceDisabled = TinkersSatchelPlugin.resources.LoadAsset<Sprite>("Assets/TinkersSatchel/Textures/ArtifactIcons/danger_off.png");
        }

        public override void Install() {
            base.Install();
            IL.RoR2.CharacterBody.RecalculateStats += IL_CBRecalcStats;
        }

        public override void Uninstall() {
            base.Uninstall();
            IL.RoR2.CharacterBody.RecalculateStats -= IL_CBRecalcStats;
        }

        

        ////// Hooks //////

        private void IL_CBRecalcStats(ILContext il) {
            ILCursor c = new ILCursor(il);
            bool ILFound = c.TryGotoNext(
                x=>x.MatchCallOrCallvirt<CharacterBody>("set_hasOneShotProtection"));
            if(ILFound) {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<bool, CharacterBody, bool>>((origSet, body)=>{
                    return body.isPlayerControlled && !IsActiveAndEnabled();
                });
            } else {
                TinkersSatchelPlugin._logger.LogError("failed to apply IL patch (Artifact of Danger, set OHP flag)! Artifact will not prevent OHP while enabled.");
            }

            ILFound = c.TryGotoNext(
                x=>x.MatchCallOrCallvirt<Mathf>("Max"),
                x=>x.MatchCallOrCallvirt<CharacterBody>("set_oneShotProtectionFraction"));
            if(ILFound) {
                c.Index++;
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<float,CharacterBody,float>>((origFrac,body)=>{return preventCurseWhileOff ? body.oneShotProtectionFraction : origFrac;});
            } else {
                TinkersSatchelPlugin._logger.LogError("failed to apply IL patch (Artifact of Danger, set OHP fraction)! Artifact's PreventCurseWhileOff config will not work.");
            }
        }
    }
}