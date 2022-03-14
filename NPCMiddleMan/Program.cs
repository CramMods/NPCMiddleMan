using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.Threading.Tasks;
using Mutagen.Bethesda.Plugins;
using System.IO;
using Mutagen.Bethesda.Plugins.Records;

namespace NPCMiddleMan
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "NPCMiddleMan.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            ModKey easyNpcModKey = new("NPC Appearances Merged", ModType.Plugin);
            bool easyNpcPatched = state.LoadOrder.PriorityOrder.HasMod(easyNpcModKey);
            if (!easyNpcPatched) throw new Exception("Not patched by EasyNPC");

            state.PatchMod.MasterReferences.Add(new() { Master = easyNpcModKey });

            ISkyrimModGetter easyNpc = SkyrimMod.CreateFromBinaryOverlay(ModPath.FromPath(Path.Combine(state.DataFolderPath, easyNpcModKey.ToString())), SkyrimRelease.SkyrimSE);
            foreach(INpcGetter npcGetter in easyNpc.Npcs.Records)
            {
                Npc npc = state.PatchMod.Npcs.GetOrAddAsOverride(npcGetter);
                string raceFormKeyString = npc.Race.FormKey.ToString();

                if (npc.WornArmor.IsNull) continue;
                IArmorGetter oldArmor = npc.WornArmor.Resolve(state.LinkCache);
                string currentId = oldArmor.EditorID ?? throw new Exception("No EditorID... weird.");

                Armor newSkin = state.PatchMod.Armors.AddNew($"{currentId}_{npc.EditorID ?? npc.FormKey.ToString()}");
                newSkin.DeepCopyIn(oldArmor, new Armor.TranslationMask(true) { EditorID = false, Armature = false });


                foreach (IFormLinkGetter<IArmorAddonGetter> addonLink in oldArmor.Armature)
                {
                    IArmorAddonGetter oldAddon = addonLink.Resolve(state.LinkCache);
                    if ( (oldAddon.Race.FormKey.ToString() == raceFormKeyString) ||
                         oldAddon.AdditionalRaces.Select(r => r.FormKey.ToString()).Contains(raceFormKeyString) )
                    {
                        newSkin.Armature.Add(oldAddon);
                        continue;
                    }
                }

                npc.WornArmor.SetTo(newSkin);
            }
        }
    }
}
