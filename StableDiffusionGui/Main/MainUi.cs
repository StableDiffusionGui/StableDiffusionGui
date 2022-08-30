﻿using StableDiffusionGui.Data;
using StableDiffusionGui.Io;
using StableDiffusionGui.Ui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StableDiffusionGui.Main
{
    internal class MainUi
    {
        public static int CurrentSteps;
        public static float CurrentScale;

        public static int CurrentResW;
        public static int CurrentResH;

        public static float CurrentInitStrength;

        public static string CurrentEmbeddingPath;

        private static readonly string[] validInitImgExtensions = new string[] { ".png", ".jpeg", ".jpg", ".jfif", ".bmp" };

        public static void HandleDroppedFiles(string[] paths)
        {
            foreach (string path in paths.Where(x => Path.GetExtension(x) == ".png"))
            {
                ImageMetadata meta = IoUtils.GetImageMetadata(path);

                if (!string.IsNullOrWhiteSpace(meta.Prompt))
                    Logger.Log($"Found metadata in {Path.GetFileName(path)}:\n{meta.ParsedText}");
            }

            if (paths.Length == 1)
            {
                if (validInitImgExtensions.Contains(Path.GetExtension(paths[0]))) // Ask to use as init img
                {
                    DialogResult dialogResult = UiUtils.ShowMessageBox($"Do you want to load this image as an initialization image?", $"Dropped {Path.GetFileName(paths[0]).Trunc(40)}", MessageBoxButtons.YesNo);

                    if (dialogResult == DialogResult.Yes)
                        Program.MainForm.TextboxInitImgPath.Text = paths[0];
                }

                if (Path.GetExtension(paths[0]) == ".pt") // Ask to use as embedding (finetuned model)
                {
                    DialogResult dialogResult = UiUtils.ShowMessageBox($"Do you want to load this embedding?", $"Dropped {Path.GetFileName(paths[0]).Trunc(40)}", MessageBoxButtons.YesNo);

                    if (dialogResult == DialogResult.Yes)
                        CurrentEmbeddingPath = paths[0];
                }
            }
        }

        public static List<float> GetScales(string customScalesText)
        {
            List<float> scales = new List<float> { CurrentScale };

            if (customScalesText.MatchesWildcard("* > * : *"))
            {
                var splitMinMax = customScalesText.Trim().Split('>');
                float valFrom = splitMinMax[0].GetFloat();
                float valTo = splitMinMax[1].Trim().GetFloat();
                float step = customScalesText.Split(':').Last().GetFloat();

                List<float> incrementScales = new List<float>();

                if (valFrom < valTo)
                {
                    for (float f = valFrom; f < (valTo + 0.01f); f += step)
                        incrementScales.Add(1f - f);
                }
                else
                {
                    for (float f = valFrom; f >= (valTo - 0.01f); f -= step)
                        incrementScales.Add(1f - f);
                }

                if (incrementScales.Count > 0)
                    scales = incrementScales; // Replace list, don't use the regular scale slider at all in this mode
            }
            else
            {
                scales.AddRange(customScalesText.Replace(" ", "").Split(",").Select(x => x.GetFloat()).Where(x => x > 0.05f));
            }

            return scales;
        }

        public static List<float> GetInitStrengths(string customStrengthsText)
        {
            List<float> strengths = new List<float> { 1f - CurrentInitStrength };

            if (customStrengthsText.MatchesWildcard("* > * : *"))
            {
                var splitMinMax = customStrengthsText.Trim().Split(':')[0].Split('>');
                float valFrom = splitMinMax[0].GetFloat();
                float valTo = splitMinMax[1].Trim().GetFloat();
                float step = customStrengthsText.Split(':').Last().GetFloat();

                List<float> incrementStrengths = new List<float>();

                if(valFrom < valTo)
                {
                    for (float f = valFrom; f < (valTo + 0.01f); f += step)
                        incrementStrengths.Add(1f - f);
                }
                else
                {
                    for (float f = valFrom; f >= (valTo - 0.01f); f -= step)
                        incrementStrengths.Add(1f - f);
                }

                if (incrementStrengths.Count > 0)
                    strengths = incrementStrengths; // Replace list, don't use the regular scale slider at all in this mode
            }
            else
            {
                strengths.AddRange(customStrengthsText.Replace(" ", "").Split(",").Select(x => x.GetFloat()).Where(x => x > 0.05f));
            }

            return strengths;
        }
    }
}