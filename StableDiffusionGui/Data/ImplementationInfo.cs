﻿using StableDiffusionGui.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StableDiffusionGui.Data
{
    public class ImplementationInfo
    {
        public Enums.Ai.Backend Backend { get; set; } = Enums.Ai.Backend.Cuda;
        public bool SupportsDeviceSelection { get; set; } = false;
        public bool IsInteractive { get; set; } = false;
        public bool HasPrecisionOpt { get; set; } = false;
        public bool SupportsCustomModels { get; set; } = true;
        public bool SupportsCustomVaeModels { get; set; } = false;
        public bool SupportsNativeInpainting { get; set; } = false;
        public bool SupportsNegativePrompt { get; set; } = false;

        public ImplementationInfo() { }

        public ImplementationInfo(Enums.StableDiffusion.Implementation imp)
        {
            if (imp == Enums.StableDiffusion.Implementation.InvokeAi)
            {
                Backend = Enums.Ai.Backend.Cuda;
                SupportsDeviceSelection = true;
                IsInteractive = true;
                HasPrecisionOpt = true;
                SupportsCustomModels = true;
                SupportsCustomVaeModels = true;
                SupportsNativeInpainting = true;
                SupportsNegativePrompt = true;
            }
            else if (imp == Enums.StableDiffusion.Implementation.OptimizedSd)
            {
                Backend = Enums.Ai.Backend.Cuda;
                SupportsDeviceSelection = true;
                IsInteractive = false;
                HasPrecisionOpt = true;
                SupportsCustomModels = true;
                SupportsCustomVaeModels = false;
            }
            else if (imp == Enums.StableDiffusion.Implementation.DiffusersOnnx)
            {
                Backend = Enums.Ai.Backend.DirectMl;
                SupportsDeviceSelection = false;
                IsInteractive = true;
                HasPrecisionOpt = true;
                SupportsCustomModels = true;
                SupportsCustomVaeModels = false;
                SupportsNativeInpainting = true;
                SupportsNegativePrompt = true;
            }
            else if (imp == Enums.StableDiffusion.Implementation.InstructPixToPix)
            {
                Backend = Enums.Ai.Backend.Cuda;
                SupportsDeviceSelection = false;
                IsInteractive = true;
                HasPrecisionOpt = false;
                SupportsCustomModels = false;
                SupportsCustomVaeModels = false;
                SupportsNegativePrompt = true;
            }
        }
    }
}
