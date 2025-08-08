using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using HuntTrack.Windows;

namespace HuntTrack;

public sealed class Plugin : IDalamudPlugin
{
    public List<Cible> Cibles { get; private set; } = new();

    // Objet HuntConfig pour conserver la version et la liste des cibles
    public HuntConfig Config { get; private set; } = new HuntConfig
    {
        Version = "1.0",
        Cibles = new List<Cible>()
    };

    public readonly string CleanFilePath =
        Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, @"Data", "HuntClean.json");

    public readonly string SaveFilePath =
        Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, @"Data", "HuntSave.json");

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ITextureProvider TextureProvider { get; private set; } = null!;
    
    [PluginService]
    public static IChatGui Chat { get; private set; } = null!;
    
    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static IClientState ClientState { get; private set; } = null!;

    [PluginService]
    internal static IDataManager DataManager { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandNameLong = "/hunttrack";
    private const string CommandNameCourt = "/ht";
    private const string CommandNameCheck = "/htcheck";
    private const string CommandNameValid = "/htvalid";


    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("HuntTrack");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);


        CommandManager.AddHandler(CommandNameLong, new CommandInfo(OnCommandWindows)
        {
            HelpMessage = "Ouvre la fenêtre du plugin"
            // HelpMessage = "A useful message to display in /xlhelp"
        });
        CommandManager.AddHandler(CommandNameCourt, new CommandInfo(OnCommandWindows)
        {
            HelpMessage = "Ouvre la fenêtre du plugin"
            // HelpMessage = "A useful message to display in /xlhelp"
        });
        CommandManager.AddHandler(CommandNameCheck, new CommandInfo(OnCommandCheck)
        {
            HelpMessage = "Vérifie si la cible a été validé"
            // HelpMessage = "A useful message to display in /xlhelp"
        });
        CommandManager.AddHandler(CommandNameValid, new CommandInfo(OnCommandValid)
        {
            HelpMessage = "Valide une cible"
            // HelpMessage = "A useful message to display in /xlhelp"
        });


        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [HuntTrack] ===A cool log message from Sample Plugin===
        LoadJson();
        
        Log.Information($"{PluginInterface.Manifest.Name} initialisé.");
        

    }
    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
            
        ConfigWindow.Dispose();
        MainWindow.Dispose();
            
        CommandManager.RemoveHandler(CommandNameLong);
        CommandManager.RemoveHandler(CommandNameCourt);
        CommandManager.RemoveHandler(CommandNameCheck);
        CommandManager.RemoveHandler(CommandNameValid);
        
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUI;
        
        Log.Information($"Fin DISPOSE du plugin.");
    }
    
    private void OnCommandWindows(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
        Log.Information($"Ouverture/Fermeture fenêtre du plugin: {command}.");
    }
    
    private void OnCommandCheck(string command, string args)
    {
        var splitArgs = args.Split(' ');

        foreach (var cibleTemp in splitArgs)
        {
            if (string.IsNullOrWhiteSpace(cibleTemp))
                return ;

            string rechercheLower = cibleTemp.ToLowerInvariant();
            List<Cible> temp = new List<Cible>();
            temp = Config.Cibles.Where(c =>
            {
                string nom = Configuration.IsEnglish ? c.Name : c.Nom;
                return nom != null && nom.ToLowerInvariant().Contains(rechercheLower);
            }).ToList();

            foreach (var tempCible in temp)
            {
                if (tempCible.Valid)
                {
                    Chat.Print($"La cible {tempCible.Name} a déjà été abattu");
                }
                else
                {
                    Chat.Print($"La cible {tempCible.Name} n'a pas encore abattu");
                }
            }
        }
        
        Log.Information($"Commande Check du plugin: {command}.");
    }
    
    private void OnCommandValid(string command, string args)
    {
        var splitArgs = args.Split(' ');

        foreach (var cibleTemp in splitArgs)
        {
            if (string.IsNullOrWhiteSpace(cibleTemp))
                return ;

            string rechercheLower = cibleTemp.ToLowerInvariant();
            List<Cible> temp = new List<Cible>();
            temp = Config.Cibles.Where(c =>
            {
                string nom = Configuration.IsEnglish ? c.Name : c.Nom;
                return nom != null && nom.ToLowerInvariant().Contains(rechercheLower);
            }).ToList();

            foreach (var tempCible in temp)
            {
                if (!tempCible.Valid)
                {
                    tempCible.Valid = true;
                    var index = Config.Cibles.FindIndex(c => c.Id == tempCible.Id);
                    if (index != -1)
                    {
                        
                        Config.Cibles[index] = tempCible;
                        UpdateCibles(Config.Cibles); // Si tu as une méthode pour enregistrer
                    }
                    Chat.Print($"La cible {tempCible.Name} a été validé");
                }
            }
        }
        Log.Information($"Commande Valid du plugin: {command}.");
    }
    

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();

    public class HuntConfig
    {
        public string Version { get; set; } = "1.0";
        public List<Cible> Cibles { get; set; } = new();
    }

    public class Cible
    {
        public String Name { get; set; } = string.Empty;
        public String Nom { get; set; } = string.Empty;
        public bool Valid { get; set; } = false;
        public String Extension { get; set; } = string.Empty;
        
        public int Id { get; set; } = 0;
        public String Rang { get; set; } = string.Empty;
        
        public String Region { get; set; } = string.Empty;
        public String Map { get; set; } = string.Empty;
    }

    public class Services
    {
        [PluginService]
        public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

        [PluginService]
        public static IChatGui Chat { get; private set; } = null!;

        [PluginService]
        public static IClientState ClientState { get; private set; } = null!;

        [PluginService]
        public static IFramework Framework { get; private set; } = null!;
    }

    
    public void UpdateCibles(List<Cible> nouvellesCibles)
    {
        // Mettre à jour la liste des cibles dans Config
        Config.Cibles = nouvellesCibles;

        // Appeler WriteJsonFile pour sauvegarder uniquement les cibles, mais avec la version du config
        WriteJsonFile(nouvellesCibles);
    }

    public void WriteJsonFile(List<Cible> cibles)
    {
        // Utiliser la version déjà présente dans l'objet Config
        var data = new HuntConfig
        {
            Version = Config.Version,
            Cibles = cibles
        };

        // Sérialiser l'objet HuntConfig avec les données mises à jour
        var jsonContent = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true,  // Pour une présentation lisible avec des indentations
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping  // Désactive l'échappement Unicode
        });

        // Écrire dans le fichier JSON à l'emplacement spécifié
        File.WriteAllText(SaveFilePath, jsonContent);

        Log.Information($"Fichier JSON sauvegardé à : {SaveFilePath}");
    }
    
    public void LoadJson()
    {
        try
        {
            if (!File.Exists(SaveFilePath))
            {
                Log.Information("HuntSave.json non trouvé. Création en cours...");
                File.Copy(CleanFilePath, SaveFilePath);
                Log.Information("HuntSave.json créé.");
            }

            // Charger les fichiers JSON
            var cleanJson = ReadJsonFile<HuntConfig>(CleanFilePath);
            var saveJson = ReadJsonFile<HuntConfig>(SaveFilePath);

            if (cleanJson == null || saveJson == null)
            {
                Console.WriteLine("Erreur : Impossible de charger les fichiers JSON.");
                Cibles = new List<Cible>(); // ️ Toujours initialiser la liste !
                return;
            }

            // Comparer les versions
            if (cleanJson.Version != saveJson.Version)
            {
                Log.Information($"Mise à jour requise : HuntSave.json ({saveJson.Version}) → ({cleanJson.Version})");

                // Mise à jour des objets `Cible` en conservant `Valid`
                foreach (var newCible in cleanJson.Cibles)
                {
                    var oldCible = saveJson.Cibles.FirstOrDefault(c => c.Id == newCible.Id);
                    if (oldCible != null)
                    {
                        newCible.Valid = oldCible.Valid;
                    }
                }

                Config.Version = cleanJson.Version;
                saveJson.Cibles = cleanJson.Cibles;
                // Sauvegarder la nouvelle version avec les valeurs de `Valid`
                WriteJsonFile(saveJson.Cibles);
                Log.Information("HuntSave.json mis à jour avec conservation de Valid !");
            }
            else
            {
                Log.Information("HuntSave.json est à jour.");
            }

            Config.Version = saveJson.Version;                    // Mettre à jour la version
            Config.Cibles = saveJson.Cibles ?? new List<Cible>(); // Si cibles est null, initialiser avec une liste vide

            Log.Information($"{Config.Cibles.Count} cibles chargées en mémoire !");
        }
        catch (Exception ex)
        {
            Log.Information($"Erreur : {ex.Message}");
            Cibles = new List<Cible>();
        }
    }

    private static T? ReadJsonFile<T>(string filePath)
    {
        if (!File.Exists(filePath)) return default;
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private readonly JsonSerializerOptions? _options = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
