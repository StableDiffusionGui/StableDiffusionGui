﻿using StableDiffusionGui.Data;
using StableDiffusionGui.Extensions;
using StableDiffusionGui.Forms;
using StableDiffusionGui.Implementations;
using StableDiffusionGui.Installation;
using StableDiffusionGui.Io;
using StableDiffusionGui.Main;
using StableDiffusionGui.Main.Utils;
using StableDiffusionGui.MiscUtils;
using StableDiffusionGui.Os;
using StableDiffusionGui.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using System.Windows.Forms;
using static StableDiffusionGui.Main.Enums.Misc;
using static StableDiffusionGui.Main.Enums.Program;
using static StableDiffusionGui.Main.Enums.StableDiffusion;

namespace StableDiffusionGui.Ui
{
    internal class MainUi
    {
        private static List<string> _currentInitImgPaths = new List<string>();
        public static List<string> CurrentInitImgPaths
        {
            get => _currentInitImgPaths;
            set
            {
                if (value == null)
                    value = new List<string>();

                _currentInitImgPaths = value;

                if (value != null && value.Count() > 0)
                {
                    Logger.Log(value.Count() == 1 ? $"Now using initialization image {Path.GetFileName(value[0]).Wrap()}." : $"Now using {value.Count()} initialization images.");

                    if (Config.Get<bool>(Config.Keys.AutoSetResForInitImg))
                        SetResolutionForInitImage(value[0]);
                }

                if (Inpainting.CurrentMask != null || Inpainting.CurrentRawMask != null)
                {
                    Inpainting.ClearMask();
                    Logger.Log("Inpainting mask has been cleared.");
                }
            }
        }

        public static List<TtiSettings> Queue = new List<TtiSettings>();
        public static string GpuInfo = "";

        public static List<int> GetResolutions(int min, int max)
        {
            int step = ConfigParser.CurrentImplementation == Implementation.InstructPixToPix ? 8 : 64;

            if (Program.Debug && ConfigParser.CurrentImplementation == Implementation.InvokeAi)
                step = 8;

            return Enumerable.Range(min, (max - min) + 1).Where(x => x % step == 0).ToList();
        }

        public static void DoStartupChecks()
        {
            if (!Program.Debug)
            {
                string dir = Paths.GetExeDir();

                List<char> nonAsciiCharsInPath = FormatUtils.GetNonAsciiChars(dir);

                if (nonAsciiCharsInPath.Count > 0)
                {
                    UiUtils.ShowMessageBox($"You are running this program from a path that contains special characters ({string.Join(", ", nonAsciiCharsInPath.Distinct())}).\n" +
                        $"Please move it to a path without special characters and try again.", UiUtils.MessageType.Error, Nmkoder.Forms.MessageForm.FontSize.Big);
                    Application.Exit();
                }

                if (dir.Lower().Replace("\\", "/").MatchesWildcard("*/users/*/onedrive/*"))
                {
                    UiUtils.ShowMessageBox($"Running this program out of the OneDrive folder is not supported. Please move it to a local drive and try again.", UiUtils.MessageType.Error, Nmkoder.Forms.MessageForm.FontSize.Big);
                    Application.Exit();
                }

                if (dir.Length > 70)
                    UiUtils.ShowMessageBox($"You are running the program from this path:\n\n{Paths.GetExeDir()}\n\nIt's very long ({dir.Length} characters), this can cause problems.\n" +
                        $"Please move the program to a shorter path or continue at your own risk.", UiUtils.MessageType.Warning, Nmkoder.Forms.MessageForm.FontSize.Big);
            }
            else
            {
                Logger.Log($"Debug mode enabled. {(System.Diagnostics.Debugger.IsAttached ? "Debugger is attached." : "")}");
            }

            if (Program.UserArgs.Get(Constants.Args.Install) == true.ToString())
            {
                Program.MainForm.BringToFront();
                bool onnx = Program.UserArgs.Get(Constants.Args.InstallOnnx) == true.ToString();
                bool upscalers = Program.UserArgs.Get(Constants.Args.InstallUpscalers) == true.ToString();
                new InstallerForm(onnx, upscalers).ShowDialogForm();
            }
            else
            {
                if (!InstallationStatus.IsInstalledBasic)
                {
                    UiUtils.ShowMessageBox("No complete installation of the Stable Diffusion files was found.\n\nThe GUI will now open the installer.\nPlease press \"Install\" in the next window to install all required files.");
                    new InstallerForm().ShowDialogForm();
                }
            }

            if (ConfigParser.CurrentImplementation.Supports(ImplementationInfo.Feature.CustomModels) && Models.GetModelsAll().Count <= 0)
                UiUtils.ShowMessageBox($"No model files have been found. You will not be able to generate images until you either place a model in Data/models, or set an external folder in the settings.",
                    UiUtils.MessageType.Warning, Nmkoder.Forms.MessageForm.FontSize.Normal);
        }

        public static bool IsInstalledWithWarning(bool showInstaller = true)
        {
            if (!InstallationStatus.IsInstalledBasic)
            {
                UiUtils.ShowMessageBox("A valid installation is required.");

                if (showInstaller)
                    new InstallerForm().ShowDialogForm();

                return false;
            }

            return true;
        }

        public static void HandleDroppedFiles(string[] paths, bool noConfirmations = false)
        {
            if (Program.Busy || paths == null || paths.Length < 1)
                return;

            if (paths.Length == 1)
            {
                if (Constants.FileExts.ValidImages.Contains(Path.GetExtension(paths[0]).Lower())) // Ask to use as init img
                {
                    ImageLoadForm imgForm = new ImageLoadForm(paths[0]);
                    imgForm.ShowDialogForm();

                    if (imgForm.Action == ImageImportAction.LoadSettings || imgForm.Action == ImageImportAction.LoadImageAndSettings)
                        Program.MainForm.LoadMetadataIntoUi(imgForm.CurrentMetadata);

                    if (imgForm.Action == ImageImportAction.LoadImage || imgForm.Action == ImageImportAction.LoadImageAndSettings)
                        AddInitImages(paths.ToList());

                    if (imgForm.Action == ImageImportAction.CopyPrompt)
                        OsUtils.SetClipboard(imgForm.CurrentMetadata.Prompt);
                }

                Program.MainForm.TryRefreshUiState();
            }
            else
            {
                paths = paths.OrderBy(path => Path.GetFileName(path)).ToArray(); // Sort by filename
                var validImagesInPathList = paths.Where(path => Constants.FileExts.ValidImages.Contains(Path.GetExtension(path).Lower()));

                if (validImagesInPathList.Any())
                {
                    DialogResult dialogResult = noConfirmations ? DialogResult.Yes : UiUtils.ShowMessageBox($"Do you want to load these images as initialization images?", $"Dropped {paths.Length} Images", MessageBoxButtons.YesNo);

                    if (dialogResult == DialogResult.Yes)
                        AddInitImages(paths.ToList());
                }
            }
        }

        public static void AddInitImages(List<string> paths, bool silent = false, DialogResult silentReplaceInsteadOfAppendResult = DialogResult.Yes)
        {
            if (paths.Count < 1)
                return;

            if (CurrentInitImgPaths.Any())
            {
                bool oldIs1 = CurrentInitImgPaths.Count == 1;
                bool newIs1 = paths.Count == 1;

                string msg = $"Do you want to replace the currently loaded {(oldIs1 ? $"image '{Path.GetFileName(CurrentInitImgPaths[0])}'" : $"{CurrentInitImgPaths.Count} images")}?\n\n" +
                    $"Press \"No\" to append {(newIs1 ? "it" : "them")} to the list instead.";
                DialogResult dialogResult = silent ? silentReplaceInsteadOfAppendResult : UiUtils.ShowMessageBox(msg, $"Replace current image{(oldIs1 ? "" : "s")}?", MessageBoxButtons.YesNo);

                if (dialogResult == DialogResult.Yes)
                    CurrentInitImgPaths = paths;
                else
                    CurrentInitImgPaths = CurrentInitImgPaths.Concat(paths).ToList();
            }
            else
            {
                CurrentInitImgPaths = paths;
            }

            Program.MainForm.TryRefreshUiState();
        }

        public static void HandlePaste()
        {
            if (!Clipboard.ContainsImage())
            {
                ((Action)(() =>
                {
                    string text = Clipboard.GetText();
                    if (text.Trim().Contains("//huggingface.co/"))
                    {
                        var split = text.Split("huggingface.co/").Last().Split('/'); // Remove domain name, then split by slashes
                        string repo = $"{split[0]}/{split[1]}"; // Take username and repo name, ignore anything after that (e.g. "/tree/main" would be ignored)
                        Program.MainForm.ModelDownloadPrompt(repo);
                    }
                })).RunInTryCatch("HandlePaste Text Error:");
            }
            else
            {
                ((Action)(() =>
                {
                    Image clipboardImg = Clipboard.GetImage();

                    if (clipboardImg == null)
                        return;

                    string savePath = Paths.GetClipboardPath(".png");
                    clipboardImg.Save(savePath);
                    HandleDroppedFiles(new string[] { savePath });
                })).RunInTryCatch("HandlePaste Image Error:");
            }
        }

        public static string SanitizePrompt(string prompt)
        {
            prompt = prompt.Remove("\""); // Don't allow "
            prompt = InvokeAiUtils.ConvertAttentionSyntax(prompt); // Convert old (multi-bracket) emphasis/attention syntax to new one (with +/-)
            return prompt;
        }

        public static List<float> GetExtraValues(string text, float sliderValue)
        {
            var values = new List<float>() { sliderValue };

            if (text.MatchesWildcard("* > * : *"))
            {
                var splitMinMax = text.Trim().Split(':')[0].Split('>');
                float valFrom = splitMinMax[0].GetFloat();
                float valTo = splitMinMax[1].Trim().GetFloat();
                float step = text.Split(':').Last().GetFloat();

                List<float> incrementValues = new List<float>();

                if (valFrom < valTo)
                {
                    for (float f = valFrom; f < (valTo + 0.01f); f += step)
                        incrementValues.Add(f);
                }
                else
                {
                    for (float f = valFrom; f >= (valTo - 0.01f); f -= step)
                        incrementValues.Add(f);
                }

                if (incrementValues.Count > 0)
                    values = incrementValues;
            }
            else if (!string.IsNullOrWhiteSpace(text))
            {
                values = text.Split(",").Select(x => x.GetFloat()).Where(x => x >= 0.05f).ToList();
            }

            return values;
        }

        public enum PromptFieldSizeMode { Expand, Collapse, Toggle }

        public static void SetPromptFieldSize(PromptFieldSizeMode sizeMode = PromptFieldSizeMode.Toggle, bool negativePromptField = false)
        {
            ((Action)(() =>
            {
                var panel = negativePromptField ? Program.MainForm.textboxPromptNeg.Parent : Program.MainForm.textboxPrompt.Parent;
                var btn = negativePromptField ? Program.MainForm.btnExpandPromptNegField : Program.MainForm.btnExpandPromptField;
                int smallHeight = negativePromptField ? 45 : 70;

                if (panel.Height == 0)
                    return;

                if (sizeMode == PromptFieldSizeMode.Toggle)
                    sizeMode = panel.Height == smallHeight ? PromptFieldSizeMode.Expand : PromptFieldSizeMode.Collapse;

                if (sizeMode == PromptFieldSizeMode.Expand)
                {
                    btn.BackgroundImage = Resources.upArrowIcon;
                    panel.Height = smallHeight * 4;
                }

                if (sizeMode == PromptFieldSizeMode.Collapse)
                {
                    btn.BackgroundImage = Resources.downArrowIcon;
                    panel.Height = smallHeight;
                }

                Program.MainForm.panelSettings.Focus();
            })).RunWithUiStopped(Program.MainForm);
        }

        public static async Task GetCudaGpus()
        {
            GpuInfo = "";
            var gpus = await GpuUtils.GetCudaGpus();
            List<string> gpuNames = gpus.Select(x => x.FullName).ToList();
            int maxGpusToListInTitle = 2;

            if (gpuNames.Count < 1)
                GpuInfo = $"No CUDA GPUs available.";
            else if (gpuNames.Count <= maxGpusToListInTitle)
                GpuInfo = $"CUDA GPU{(gpuNames.Count != 1 ? "s" : "")}: {string.Join(", ", gpuNames)}";
            else
                GpuInfo = $"CUDA GPUs: {string.Join(", ", gpuNames.Take(maxGpusToListInTitle))} (+{gpuNames.Count - maxGpusToListInTitle})";

            Logger.Log($"Detected {gpus.Count.ToString().Replace("0", "no")} CUDA-capable GPU{(gpus.Count != 1 ? "s" : "")}.");
            Program.MainForm.UpdateWindowTitle();
        }

        public static async Task PrintVersion()
        {
            string ver = await GetWebInfo.LoadVersion();
            Logger.Log($"Latest version: {ver}");

            if (ver.Trim() != Program.Version)
                Logger.Log($"It seems like you are not running the latest version.{(Program.ReleaseChannel == UpdateChannel.Public ? $" You can download the latest using the updater (check the toolbar on the top right) or on itch: {Constants.Urls.ItchPage}" : "")}");
            else
                Logger.Log($"You are running the latest version ({Program.ReleaseChannel} Channel).");
        }

        public static Size GetPreferredSize()
        {
            Size outputImgSize = new Size();

            if (Program.MainForm.pictBoxImgViewer.Image == null)
            {
                if (Program.MainForm.pictBoxInitImg.Image == null)
                    return Size.Empty;
                else
                    outputImgSize = Program.MainForm.pictBoxInitImg.GetImageSafe().Size;
            }
            else
            {
                outputImgSize = Program.MainForm.pictBoxImgViewer.GetImageSafe().Size;
            }

            int picInWidth = Program.MainForm.tableLayoutPanelImgViewers.ColumnStyles[0].Width > 1 ? outputImgSize.Width : 0;
            int picOutWidth = outputImgSize.Width;
            int picOutHeight = outputImgSize.Height;
            int formWidthWithoutImgViewer = Program.MainForm.Size.Width - Program.MainForm.tableLayoutPanelImgViewers.Width;
            int formHeightWithoutImgViewer = Program.MainForm.Size.Height - Program.MainForm.tableLayoutPanelImgViewers.Height;

            Size targetSize = new Size(picInWidth + picOutWidth + formWidthWithoutImgViewer, picOutHeight.Clamp(512, 8192) + formHeightWithoutImgViewer);
            Size currScreenSize = Screen.FromControl(Program.MainForm).Bounds.Size;

            if (Program.MainForm.Size == targetSize)
                return Size.Empty;

            if (targetSize.Width > currScreenSize.Width || targetSize.Height > currScreenSize.Height)
                return Size.Empty;

            return targetSize;
        }

        public static void FitWindowSizeToImageSize()
        {
            ((Action)(() =>
            {
                Size targetSize = GetPreferredSize();

                if (targetSize == Size.Empty || Program.MainForm.Size == targetSize)
                    return;

                if (Program.MainForm.WindowState == FormWindowState.Maximized)
                    Program.MainForm.WindowState = FormWindowState.Normal;

                Program.MainForm.Size = targetSize;
            })).RunWithUiStopped(Program.MainForm);
        }

        public static void LoadAutocompleteData(AutocompleteMenuNS.AutocompleteMenu menu, TextBox textbox)
        {
            LoadAutocompleteData(menu, new[] { textbox });
        }

        public static void LoadAutocompleteData(AutocompleteMenuNS.AutocompleteMenu menu, IEnumerable<TextBox> textboxes)
        {
            foreach (TextBox textbox in textboxes)
            {
                List<string> autoCompleteStrings = new List<string>();

                autoCompleteStrings.AddRange(IoUtils.GetFileInfosSorted(Path.Combine(Paths.GetExeDir(), "Wildcards")).Select(x => $"{x.NameNoExt()}"));
                // autoCompleteStrings.AddRange(IoUtils.GetFileInfosSorted(Path.Combine(Paths.GetExeDir(), "Wildcards")).Select(x => $"~{x.NameNoExt()}"));
                // autoCompleteStrings.AddRange(IoUtils.GetFileInfosSorted(Path.Combine(Paths.GetExeDir(), "Wildcards")).Select(x => $"~~{x.NameNoExt()}"));
                // autoCompleteStrings.AddRange(IoUtils.GetFileInfosSorted(Path.Combine(Paths.GetExeDir(), "Wildcards")).Select(x => $"~~~{x.NameNoExt()}"));

                menu.Items = autoCompleteStrings.ToArray();
            }
        }

        public static string[] GetAutocompleteStrings()
        {
            List<string> strings = new List<string>();
            strings.AddRange(IoUtils.GetFileInfosSorted(Path.Combine(Paths.GetExeDir(), "Wildcards")).Select(x => $"{x.NameNoExt()}"));
            return strings.ToArray();
        }

        public static AutocompleteMenuNS.AutocompleteMenu ShowAutocompleteMenu(TextBox textbox)
        {
            var menu = MakeAutocompleteMenu(textbox.Font);
            menu.Show(textbox, true);
            return menu;
        }

        public static AutocompleteMenuNS.AutocompleteMenu MakeAutocompleteMenu(Font font)
        {
            AutocompleteMenuNS.AutocompleteMenu menu = new AutocompleteMenuNS.AutocompleteMenu();
            menu.AllowsTabKey = true;
            menu.AppearInterval = 250;
            menu.Colors = ((AutocompleteMenuNS.Colors)(new ResourceManager(typeof(StableDiffusionGui.Forms.MainForm)).GetObject("promptAutocomplete.Colors")));
            menu.Font = font;
            menu.Items = GetAutocompleteStrings();
            menu.LeftPadding = 0;
            menu.MaximumSize = new Size(300, 100);
            menu.MinFragmentLength = 100;
            menu.SearchPattern = "[\\w\\.-]";
            menu.TargetControlWrapper = null;
            return menu;
        }

        public static void SetResolutionForInitImage(string initImgPath)
        {
            Size newRes = GetResolutionForInitImage(IoUtils.GetImage(initImgPath).Size);
            Program.MainForm.comboxResW.Text = newRes.Width.ToString();
            Program.MainForm.comboxResH.Text = newRes.Height.ToString();
        }

        public static Size GetResolutionForInitImage(Size imageSize)
        {
            return ImgUtils.GetValidSize(imageSize, GetValidImageWidths(), GetValidImageHeights());
        }

        public static List<int> GetValidImageWidths()
        {
            return Program.MainForm.comboxResW.Items.Cast<string>().Select(x => x.GetInt()).ToList();
        }

        public static List<int> GetValidImageHeights()
        {
            return Program.MainForm.comboxResH.Items.Cast<string>().Select(x => x.GetInt()).ToList();
        }
    }
}
