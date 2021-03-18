// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder.Dialogs;

namespace PixelDrHistoryBot
{
    public class PixelDrHistoryBotState : DialogState
    {
    }

    public class UserHistoryState
    {
        public string patientAMTSAddress { get; set; }
    }
}
