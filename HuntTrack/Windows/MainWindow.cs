using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using ImGuiNET;

namespace HuntTrack.Windows;

public class MainWindow : Window, IDisposable
{
    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;
    private readonly Dictionary<string, string> extensionsDict = new Dictionary<string, string>
    {
        { "ARR", "A Realm Reborn" },
        { "HW", "Heavensward" },
        { "SB", "Stormblood" },
        { "ShB", "Shadowbringers" },
        { "EW", "Endwalker" },
        { "DT", "Dawntrail" }
    };
    
    private readonly string[] availableExtensions = ["ARR", "HW", "SB", "ShB", "EW", "DT"];
    private readonly string[] availableRangs = ["SS", "S", "A", "B", "Fate"];
    private readonly string[] validFilterOptions = ["Tout", "Valides", "Non Valides"];
    
    private readonly HashSet<string> selectedExtensions = [];
    private readonly HashSet<string> selectedRangs = [];
    private int validFilterIndex = 0; // 0 = Tous, 1 = Valides, 2 = Non valides
    private bool isSortedAscending = true;
    private int sortedColumnIndex = -1;
    
    private Plugin Plugin;

    private readonly ImGuiTableFlags tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable;
    private List<Plugin.Cible> cibles = new List<Plugin.Cible>();
   // private static string jsonFilePath = Plugin.SaveFilePath;

    private Configuration Configuration;
    
    // We give this window a hidden ID using ##
    // So that the user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin)
        : base("Liste des choses à faire##HuntTrack")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(455, 430),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        
        Configuration = plugin.Configuration;
        Plugin = plugin;
        
    }

    public void Dispose() { }

    public override void Draw()
    {

        cibles = Plugin.Config.Cibles;

        RenderCibles();
    }

    public void RenderCibles()
    {
      // **Filtre Extension**
       ImGui.Text("Filtrer par Extension :");
       ImGui.SameLine();
       foreach (var ext in availableExtensions)
       {
           bool isSelected = selectedExtensions.Contains(ext);
           if (ImGui.Checkbox($" {extensionsDict[ext]}", ref isSelected))
           {
               if (isSelected)
                   selectedExtensions.Add(ext);
               else
                   selectedExtensions.Remove(ext);
           }
           ImGui.SameLine(); // 🔹 Garde les éléments sur la même ligne
       }
       
       // **Nouvelle ligne avant le filtre Rang**
       ImGui.NewLine();
       
       // **Filtre Rang**
       ImGui.Text("Filtrer par Rang :");
       ImGui.SameLine();
       foreach (var rang in availableRangs)
       {
           bool isSelected = selectedRangs.Contains(rang);
           if (ImGui.Checkbox($" {rang}", ref isSelected))
           {
               if (isSelected)
                   selectedRangs.Add(rang);
               else
                   selectedRangs.Remove(rang);
           }
           ImGui.SameLine(); // 🔹 Garde les éléments sur la même ligne
       }
       
       // **Nouvelle ligne avant le filtre Valide**
       ImGui.NewLine();
       
        // **Filtre Valide**
       ImGui.Text("Filtrer par Validité :");
       ImGui.SameLine();

        // Calculer la largeur minimale en fonction du texte le plus long
       float comboWidth = 0;
       foreach (var option in validFilterOptions)
       {
           comboWidth = Math.Max(comboWidth, ImGui.CalcTextSize(option).X);
       }

        // Appliquer la largeur minimale
       ImGui.SetNextItemWidth(comboWidth + 50); // Ajoute un petit padding pour un espace agréable

       ImGui.Combo("##ValidFilter", ref validFilterIndex, validFilterOptions, validFilterOptions.Length);
       ImGui.NewLine();

        // **Affichage du tableau**
        if (ImGui.BeginTable("CiblesTable", 5, tableFlags, new Vector2(0, 300)))
        {
            ImGui.TableSetupColumn("Valide", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Nom", ImGuiTableColumnFlags.None);
            ImGui.TableSetupColumn("Extension", ImGuiTableColumnFlags.None);
            ImGui.TableSetupColumn("Rang", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Region", ImGuiTableColumnFlags.None);
            ImGui.TableHeadersRow();
            
            // Gestion du tri
            ImGuiTableSortSpecsPtr sortSpecs = ImGui.TableGetSortSpecs();
            if (sortSpecs.SpecsDirty)
            {
                sortedColumnIndex = sortSpecs.Specs.ColumnIndex;
                isSortedAscending = (sortSpecs.Specs.SortDirection == ImGuiSortDirection.Ascending);
                sortSpecs.SpecsDirty = false;

                // Tri personnalisé
                cibles.Sort((a, b) =>
                {
                    int result = 0;
                    switch (sortedColumnIndex)
                    {
                        case 0: result = a.Valid.CompareTo(b.Valid); break; // Tri sur le booléen
                        case 1: 
                            if (Configuration.IsEnglish)
                                result = string.Compare(a.Name, b.Name, StringComparison.Ordinal); 
                            else
                                result = string.Compare(a.Nom, b.Nom, StringComparison.Ordinal); 
                            break;
                        case 2: result = ComparerExtension(a.Extension, b.Extension); break;
                        case 3: result = ComparerRang(a.Rang, b.Rang); break;
                        case 4: 
                            if (Configuration.IsEnglish)
                                result = string.Compare(a.Map, b.Map, StringComparison.Ordinal); 
                            else
                                result = string.Compare(a.Region, b.Region, StringComparison.Ordinal); 
                            break;
                    }
                    return isSortedAscending ? result : -result;
                });
            }

            foreach (var cible in cibles)
            {
                // Appliquer le filtre Extension
                if (selectedExtensions.Count > 0 && !selectedExtensions.Contains(cible.Extension))
                    continue;

                // Appliquer le filtre Rang
                if (selectedRangs.Count > 0 && !selectedRangs.Contains(cible.Rang))
                    continue;

                // Appliquer le filtre Validité
                if (validFilterIndex == 1 && !cible.Valid) continue; // Valides uniquement
                if (validFilterIndex == 2 && cible.Valid) continue;  // Non valides uniquement

                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                
                // Centrer la Checkbox dans la cellule de la colonne "Validé"
                float columnWidth = ImGui.GetColumnWidth();
                float checkboxWidth = 20f; // Largeur approximative de la Checkbox
                float positionX = (columnWidth - checkboxWidth) * 0.5f; // Calculer le positionnement horizontal pour centrer
                ImGui.SetCursorPosX(positionX); // Appliquer la position calculée

                
                ImGui.PushID(cible.Id);
                bool checkedState = cible.Valid;
                if (ImGui.Checkbox("##valid", ref checkedState))
                {
                    cible.Valid = checkedState;
                    Plugin.UpdateCibles(cibles);
                }

                ImGui.PopID();

                ImGui.TableSetColumnIndex(1);
                TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;
                if (Configuration.IsEnglish)
                    ImGui.Text(textInfo.ToTitleCase(cible.Name));
                else
                    ImGui.Text(textInfo.ToTitleCase(cible.Nom));

                ImGui.TableSetColumnIndex(2);
                string extensionFull = extensionsDict.ContainsKey(cible.Extension)
                                           ? extensionsDict[cible.Extension]
                                           : cible.Extension;
                ImGui.Text(extensionFull);

                ImGui.TableSetColumnIndex(3);
                ImGui.Text(cible.Rang);

                ImGui.TableSetColumnIndex(4);
                
                if (Configuration.IsEnglish)
                    ImGui.Text(textInfo.ToTitleCase(cible.Map));
                else
                    ImGui.Text(textInfo.ToTitleCase(cible.Region));

            }
            ImGui.EndTable();
        }
    }
    
    private static int ComparerRang(string rangA, string rangB)
    {
        Dictionary<string, int> rangOrder = new Dictionary<string, int> { { "SS", 5 }, { "S", 4 }, { "A", 3 }, { "B", 2 }, { "Fate", 1 } };
        return rangOrder[rangA].CompareTo(rangOrder[rangB]);
    }
    
    private static int ComparerExtension(string rangA, string rangB)
    {
        Dictionary<string, int> rangOrder = new Dictionary<string, int> { { "DT", 6 }, { "EW", 5 }, { "ShB", 4 }, { "SB", 3 }, { "HW", 2 }, { "ARR", 1 } };
        return rangOrder[rangA].CompareTo(rangOrder[rangB]);
    }

   /* public void commentairedansledraw()
    {
        
    // Do not use .Text() or any other formatted function like TextWrapped(), or SetTooltip().
    // These expect formatting parameter if any part of the text contains a "%", which we can't
    // provide through our bindings, leading to a Crash to Desktop.
    // Replacements can be found in the ImGuiHelpers Class
    //ImGui.TextUnformatted($"The random config bool is {Plugin.Configuration.SomePropertyToBeSavedAndWithADefault}");

    if (ImGui.Button("Show Settings"))
    {
        Plugin.ToggleConfigUI();
    }

    ImGui.Spacing();

    // Normally a BeginChild() would have to be followed by an unconditional EndChild(),
    // ImRaii takes care of this after the scope ends.
    // This works for all ImGui functions that require specific handling, examples are BeginTable() or Indent().
    using (var child = ImRaii.Child("SomeChildWithAScrollbar", Vector2.Zero, true))
    {
        // Check if this child is drawing
        if (child.Success)
        {
            ImGui.TextUnformatted("Have a goat:");
            var goatImage = Plugin.TextureProvider.GetFromFile(GoatImagePath).GetWrapOrDefault();
            if (goatImage != null)
            {
                using (ImRaii.PushIndent(55f))
                {
                    ImGui.Image(goatImage.ImGuiHandle, new Vector2(goatImage.Width, goatImage.Height));
                }
            }
            else
            {
                ImGui.TextUnformatted("Image not found.");
            }

            ImGuiHelpers.ScaledDummy(20.0f);

            // Example for other services that Dalamud provides.
            // ClientState provides a wrapper filled with information about the local player object and client.

            var localPlayer = Plugin.ClientState.LocalPlayer;
            if (localPlayer == null)
            {
                ImGui.TextUnformatted("Our local player is currently not loaded.");
                return;
            }

            if (!localPlayer.ClassJob.IsValid)
            {
                ImGui.TextUnformatted("Our current job is currently not valid.");
                return;
            }

            // ExtractText() should be the preferred method to read Lumina SeStrings,
            // as ToString does not provide the actual text values, instead gives an encoded macro string.
            ImGui.TextUnformatted($"Our current job is ({localPlayer.ClassJob.RowId}) \"{localPlayer.ClassJob.Value.Abbreviation.ExtractText()}\"");

            // Example for quarrying Lumina directly, getting the name of our current area.
            var territoryId = Plugin.ClientState.TerritoryType;
            if (Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territoryRow))
            {
                ImGui.TextUnformatted($"We are currently in ({territoryId}) \"{territoryRow.PlaceName.Value.Name.ExtractText()}\"");
            }
            else
            {
                ImGui.TextUnformatted("Invalid territory.");
            }
        }
    }*/
    
}
