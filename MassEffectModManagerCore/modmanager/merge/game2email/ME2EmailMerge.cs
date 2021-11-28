using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using ME3TweaksCore.GameFilesystem;
using ME3TweaksCoreWPF;
using ME3TweaksModManager.modmanager.merge.dlc;
using Newtonsoft.Json;
using BioStateEventMap = LegendaryExplorerCore.Unreal.BinaryConverters.BioStateEventMap;

namespace ME3TweaksModManager.modmanager.merge.game2email
{
    public class ME2EmailMerge
    {
        public const int STARTING_EMAIL_CONDITIONAL = 10100;
        private const int STARTING_EMAIL_TRANSITION = 90000;
        private const string EMAIL_MERGE_MANIFEST_FILE = @"Game2EmailMerge.json";

        internal class ME2EmailMergeFile
        {
            /// <summary>
            /// Game for emails, either ME2 or LE2
            /// </summary>
            [JsonProperty("game")]
            public MEGame Game { get; set; }

            /// <summary>
            /// Name of mod, used for sequence comments only
            /// </summary>
            [JsonProperty("modName")]
            public string ModName { get; set; }

            /// <summary>
            /// Optional: id of pre-existing in memory bool indicating mod is installed. Adds extra sanity check when email is sent.
            /// Email will only send when this bool is true, if field is included
            /// </summary>
            [JsonProperty("inMemoryBool")]
            public int? InMemoryBool { get; set; }

            [JsonProperty("emails")]
            public List<ME2EmailSingle> Emails { get; set; }
        }

        internal class ME2EmailSingle
        {
            /// <summary>
            /// Internal name for email, used for sequence comments only
            /// </summary>
            [JsonProperty("emailName")]
            public string EmailName { get; set; }

            /// <summary>
            /// The text of the conditional that will trigger the email. Must include StatusPlotInt == 0, rest is up to user
            /// </summary>
            [JsonProperty("triggerConditional")]
            public string TriggerConditional { get; set; }

            /// <summary>
            /// The plot int for your email. User must determine this themselves, as it is written to save
            /// </summary>
            [JsonProperty("statusPlotInt")]
            public int StatusPlotInt { get; set; }

            [JsonProperty("titleStrRef")]
            public int TitleStrRef { get; set; }

            [JsonProperty("descStrRef")]
            public int DescStrRef { get; set; }

            /// <summary>
            /// Optional, transition id to be triggered once email is read.
            /// We might be able to get rid of this, not sure how useful it will be
            /// </summary>
            [JsonProperty("readTransition")]
            public int? ReadTransition { get; set; }
        }

        /// <summary>
        /// Writes the conditional to the startup package, returning the conditional id
        /// </summary>
        /// <param name=""></param>
        /// <param name="function"></param>
        /// <returns></returns>
        private static int WriteConditional(string functionText)
        {
            return 1;
        }

        /// <summary>
        /// Creates a Send Email transition for the given int in the state event map, returning the transition id
        /// </summary>
        /// <param name="map"></param>
        /// <param name="integerId"></param>
        /// <returns></returns>
        private static int WriteTransition(BioStateEventMap map, int integerId)
        {
            var transition = new BioStateEventMap.BioStateEvent();
            transition.Elements = new List<BioStateEventMap.BioStateEventElement>();

            // Set Unread_Messages_Exist to true
            transition.Elements.Add(new BioStateEventMap.BioStateEventElementBool
            {
                GlobalBool = 4328,
                NewState = true,
                Type = BioStateEventMap.BioStateEventElementType.Bool
            });

            // Set Yeoman_Needs_To_Comment to true
            transition.Elements.Add(new BioStateEventMap.BioStateEventElementBool
            {
                GlobalBool = 4321,
                NewState = true,
                Type = BioStateEventMap.BioStateEventElementType.Bool
            });

            // Set email int to 1 (sent and unread)
            transition.Elements.Add(new BioStateEventMap.BioStateEventElementInt
            {
                GlobalInt = integerId,
                NewValue = 1,
                InstanceVersion = 1,
                Type = BioStateEventMap.BioStateEventElementType.Int
            });

            transition.ID = map.StateEvents.Any() ? map.StateEvents.Last().ID + 1 : STARTING_EMAIL_TRANSITION;
            map.StateEvents.Add(transition);

            return transition.ID;
        }

        public static void RunGame2EmailMerge(GameTargetWPF target)
        {
            M3MergeDLC.RemoveMergeDLC(target);
            var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(target.Game, gameRootOverride: target.TargetPath);

            // File to base modifications on
            using IMEPackage pcc = MEPackageHandler.OpenMEPackage(loadedFiles[@"BioD_Nor103_Messages.pcc"]);

            // Path to Message templates file - different files for ME2/LE2
            string ResourcesFilePath = $@"ME3TweaksModManager.modmanager.emailmerge.{target.Game}.103Message_Template_{target.Game}";
            using IMEPackage resources = MEPackageHandler.OpenMEPackageFromStream(M3Utilities.GetResourceStream(ResourcesFilePath));

            // Startup file to place conditionals and transitions into
            using IMEPackage startup = MEPackageHandler.OpenMEPackageFromStream(M3Utilities.GetResourceStream(
                $@"ME3TweaksModManager.modmanager.mergedlc.{target.Game}.Startup_{M3MergeDLC.MERGE_DLC_FOLDERNAME}.pcc"));


            var emailInfos = new List<ME2EmailMergeFile>();
            var jsonSupercedances = M3Directories.GetFileSupercedances(target, new[] { @".json" });
            if (jsonSupercedances.TryGetValue(EMAIL_MERGE_MANIFEST_FILE, out var jsonList))
            {
                jsonList.Reverse();
                foreach (var dlc in jsonList)
                {
                    var jsonFile = Path.Combine(M3Directories.GetDLCPath(target), dlc, target.Game.CookedDirName(),
                        EMAIL_MERGE_MANIFEST_FILE);
                    emailInfos.Add(JsonConvert.DeserializeObject<ME2EmailMergeFile>(File.ReadAllText(jsonFile)));
                }
            }

            // Sanity checks
            if (!emailInfos.Any() || !emailInfos.SelectMany(e => e.Emails).Any())
            {
                return;
            }

            if (emailInfos.Any(e => e.Game != target.Game))
            {
                throw new Exception("Game 2 email merge manifest targets incorrect game");
            }

            // Startup File
            // Could replace this with full instanced path in M3 implementation
            ExportEntry stateEventMapExport = startup.Exports
                .First(e => e.ClassName == @"BioStateEventMap" && e.ObjectName == @"StateTransitionMap");
            BioStateEventMap StateEventMap = stateEventMapExport.GetBinaryData<BioStateEventMap>();
            ExportEntry ConditionalClass =
                startup.FindExport($@"PlotManager{M3MergeDLC.MERGE_DLC_FOLDERNAME}.BioAutoConditionals");

            #region Sequence Exports
            // Send message - All email conditionals are checked and emails transitions are triggered
            ExportEntry SendMessageContainer = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Send_Messages");
            ExportEntry LastSendMessage = KismetHelper.GetSequenceObjects(SendMessageContainer).OfType<ExportEntry>()
                .FirstOrDefault(e =>
                {
                    var outbound = KismetHelper.GetOutboundLinksOfNode(e);
                    return outbound.Count == 1 && outbound[0].Count == 0;
                });
            ExportEntry TemplateSendMessage = resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Send_MessageTemplate");
            ExportEntry TemplateSendMessageBoolCheck = resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Send_MessageTemplate_BoolCheck");


            // Mark Read - email ints are set to read
            // This is the only section that does not gracefully handle different DLC installations - DLC_CER is required atm
            ExportEntry MarkReadContainer = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_Read");
            ExportEntry LastMarkRead = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_Read.DLC_CER");
            ExportEntry MarkReadOutLink = KismetHelper.GetOutboundLinksOfNode(LastMarkRead)[0][0].LinkedOp as ExportEntry;
            KismetHelper.RemoveOutputLinks(LastMarkRead);

            ExportEntry TemplateMarkRead = resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_ReadTemplate");
            ExportEntry TemplateMarkReadTransition = resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_Read_Transition");


            // Display Messages - Str refs are passed through to GUI
            ExportEntry DisplayMessageContainer =
                pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Display_Messages");
            ExportEntry DisplayMessageOutLink =
                pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Display_Messages.SeqCond_CompareBool_0");

            ExportEntry LastDisplayMessage = SeqTools.FindOutboundConnectionsToNode(DisplayMessageOutLink, KismetHelper.GetSequenceObjects(DisplayMessageContainer).OfType<ExportEntry>())[0];
            KismetHelper.RemoveOutputLinks(LastDisplayMessage);
            var DisplayMessageVariableLinks = LastDisplayMessage.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            ExportEntry TemplateDisplayMessage =
                resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Display_MessageTemplate");

            // Archive Messages - Message ints are set to 3
            ExportEntry ArchiveContainer = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Archive_Message");
            ExportEntry ArchiveSwitch = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Archive_Message.SeqAct_Switch_0");
            ExportEntry ArchiveOutLink =
                pcc.FindExport(
                    @"TheWorld.PersistentLevel.Main_Sequence.Archive_Message.BioSeqAct_PMCheckConditional_1");
            ExportEntry ExampleSetInt = KismetHelper.GetOutboundLinksOfNode(ArchiveSwitch)[0][0].LinkedOp as ExportEntry;
            ExportEntry ExamplePlotInt = SeqTools.GetVariableLinksOfNode(ExampleSetInt)[0].LinkedNodes[0] as ExportEntry;
            #endregion

            int messageID = KismetHelper.GetOutboundLinksOfNode(ArchiveSwitch).Count + 1;
            int currentSwCount = ArchiveSwitch.GetProperty<IntProperty>("LinkCount").Value;

            foreach (var emailMod in emailInfos)
            {
                string modName = @"DLC_MOD_" + emailMod.ModName;

                foreach (var email in emailMod.Emails)
                {
                    string emailName = modName + "_" + email.EmailName;

                    // Create send transition
                    int transitionId = WriteTransition(StateEventMap, email.StatusPlotInt);
                    int conditionalId = WriteConditional(email.TriggerConditional);

                    #region SendMessage
                    //////////////
                    // SendMessage
                    //////////////

                    // Create seq object
                    var SMTemp = emailMod.InMemoryBool.HasValue ? TemplateSendMessageBoolCheck : TemplateSendMessage;
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild,
                        SMTemp,
                        pcc, SendMessageContainer, true, new RelinkerOptionsPackage(), out var outSendEntry);

                    var newSend = outSendEntry as ExportEntry;

                    // Set name, comment, add to sequence
                    newSend.ObjectName = new NameReference(emailName);
                    KismetHelper.AddObjectToSequence(newSend, SendMessageContainer);
                    KismetHelper.SetComment(newSend, emailName);
                    if (target.Game == MEGame.ME2) newSend.WriteProperty(new StrProperty(emailName, "ObjName"));

                    // Set Trigger Conditional
                    var pmCheckConditionalSM = newSend.GetChildren()
                        .FirstOrDefault(e => e.ClassName == "BioSeqAct_PMCheckConditional" && e is ExportEntry);
                    if (pmCheckConditionalSM is ExportEntry conditional)
                    {
                        conditional.WriteProperty(new IntProperty(conditionalId, "m_nIndex"));
                        KismetHelper.SetComment(conditional, "Time for " + email.EmailName + "?");
                    }

                    // Set Send Transition
                    var pmExecuteTransitionSM = newSend.GetChildren()
                        .FirstOrDefault(e => e.ClassName == "BioSeqAct_PMExecuteTransition" && e is ExportEntry);
                    if (pmExecuteTransitionSM is ExportEntry transition)
                    {
                        transition.WriteProperty(new IntProperty(transitionId, "m_nIndex"));
                        KismetHelper.SetComment(transition, "Send " + email.EmailName + " message.");
                    }

                    // Set Send Transition
                    if (emailMod.InMemoryBool.HasValue)
                    {
                        var pmCheckStateSM = newSend.GetChildren()
                            .FirstOrDefault(e => e.ClassName == "BioSeqAct_PMCheckState" && e is ExportEntry);
                        if (pmCheckStateSM is ExportEntry checkState)
                        {
                            checkState.WriteProperty(new IntProperty(emailMod.InMemoryBool.Value, "m_nIndex"));
                            KismetHelper.SetComment(checkState, "Is " + emailMod.ModName + " installed?");
                        }
                    }

                    // Hook up output links
                    KismetHelper.CreateOutputLink(LastSendMessage, "Out", newSend);
                    LastSendMessage = newSend;
                    #endregion

                    #region MarkRead
                    ///////////
                    // MarkRead
                    ///////////

                    // Create seq object
                    var MRTemp = email.ReadTransition.HasValue ? TemplateMarkReadTransition : TemplateMarkRead;
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild,
                        MRTemp, pcc, MarkReadContainer, true, new RelinkerOptionsPackage(), out var outMarkReadEntry);
                    var newMarkRead = outMarkReadEntry as ExportEntry;

                    // Set name, comment, add to sequence
                    newMarkRead.ObjectName = new NameReference(emailName);
                    KismetHelper.AddObjectToSequence(newMarkRead, MarkReadContainer);
                    KismetHelper.SetComment(newMarkRead, emailName);
                    if (target.Game == MEGame.ME2) newMarkRead.WriteProperty(new StrProperty(emailName, "ObjName"));

                    // Set Plot Int
                    var storyManagerIntMR = newMarkRead.GetChildren()
                        .FirstOrDefault(e => e.ClassName == "BioSeqVar_StoryManagerInt" && e is ExportEntry);
                    if (storyManagerIntMR is ExportEntry plotIntMR)
                    {
                        plotIntMR.WriteProperty(new IntProperty(email.StatusPlotInt, "m_nIndex"));
                        KismetHelper.SetComment(plotIntMR, email.EmailName);
                    }

                    if (email.ReadTransition.HasValue)
                    {
                        var pmExecuteTransitionMR = newMarkRead.GetChildren()
                            .FirstOrDefault(e => e.ClassName == "BioSeqAct_PMExecuteTransition" && e is ExportEntry);
                        if (pmExecuteTransitionMR is ExportEntry transitionMR)
                        {
                            transitionMR.WriteProperty(new IntProperty(email.ReadTransition.Value, "m_nIndex"));
                            KismetHelper.SetComment(transitionMR, "Trigger " + email.EmailName + " read transition");
                        }
                    }

                    // Hook up output links
                    KismetHelper.CreateOutputLink(LastMarkRead, "Out", newMarkRead);
                    LastMarkRead = newMarkRead;
                    #endregion

                    #region DisplayEmail
                    ////////////////
                    // Display Email
                    ////////////////

                    // Create seq object
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild,
                        TemplateDisplayMessage, pcc, DisplayMessageContainer, true, new RelinkerOptionsPackage(), out var outDisplayMessage);
                    var newDisplayMessage = outDisplayMessage as ExportEntry;

                    // Set name, comment, variable links, add to sequence
                    newDisplayMessage.ObjectName = new NameReference(emailName);
                    KismetHelper.AddObjectToSequence(newDisplayMessage, DisplayMessageContainer);
                    newDisplayMessage.WriteProperty(DisplayMessageVariableLinks);
                    KismetHelper.SetComment(newDisplayMessage, emailName);
                    if (target.Game == MEGame.ME2) newDisplayMessage.WriteProperty(new StrProperty(emailName, "ObjName"));

                    var displayChildren = newDisplayMessage.GetChildren();

                    // Set Plot Int
                    var storyManagerIntDE = displayChildren.FirstOrDefault(e =>
                        e.ClassName == "BioSeqVar_StoryManagerInt" && e is ExportEntry);
                    if (storyManagerIntDE is ExportEntry plotIntDE)
                    {
                        plotIntDE.WriteProperty(new IntProperty(email.StatusPlotInt, "m_nIndex"));
                    }

                    // Set Email ID
                    var emailIdDE = displayChildren.FirstOrDefault(e =>
                        e.ClassName == "SeqVar_Int" && e is ExportEntry);
                    if (emailIdDE is ExportEntry EmailIDDE)
                    {
                        EmailIDDE.WriteProperty(new IntProperty(messageID, "IntValue"));
                    }

                    // Set Title StrRef
                    var titleStrRef = displayChildren.FirstOrDefault(e =>
                        e.ClassName == "BioSeqVar_StrRef" && e is ExportEntry ee && ee.GetProperty<NameProperty>("VarName").Value == "Title StrRef");
                    if (titleStrRef is ExportEntry Title)
                    {
                        Title.WriteProperty(new StringRefProperty(email.TitleStrRef, "m_srValue"));
                    }

                    // Set Description StrRef
                    var descStrRef = displayChildren.FirstOrDefault(e =>
                        e.ClassName == "BioSeqVar_StrRef" && e is ExportEntry ee && ee.GetProperty<NameProperty>("VarName").Value == "Desc StrRef");
                    if (descStrRef is ExportEntry Desc)
                    {
                        Desc.WriteProperty(new StringRefProperty(email.DescStrRef, "m_srValue"));
                    }

                    // Hook up output links
                    KismetHelper.CreateOutputLink(LastDisplayMessage, "Out", newDisplayMessage);
                    LastDisplayMessage = newDisplayMessage;
                    #endregion

                    #region ArchiveEmail
                    ////////////////
                    // Archive Email
                    ////////////////

                    var NewSetInt = EntryCloner.CloneEntry(ExampleSetInt);
                    KismetHelper.AddObjectToSequence(NewSetInt, ArchiveContainer);
                    KismetHelper.CreateOutputLink(NewSetInt, "Out", ArchiveOutLink);

                    KismetHelper.CreateNewOutputLink(ArchiveSwitch, "Link " + (messageID - 1), NewSetInt);

                    var NewPlotInt = EntryCloner.CloneEntry(ExamplePlotInt);
                    KismetHelper.AddObjectToSequence(NewPlotInt, ArchiveContainer);
                    NewPlotInt.WriteProperty(new IntProperty(email.StatusPlotInt, "m_nIndex"));
                    NewPlotInt.WriteProperty(new StrProperty(emailName, "m_sRefName"));

                    var linkedVars = SeqTools.GetVariableLinksOfNode(NewSetInt);
                    linkedVars[0].LinkedNodes = new List<IEntry>() { NewPlotInt };
                    SeqTools.WriteVariableLinksToNode(NewSetInt, linkedVars);

                    messageID++;
                    currentSwCount++;
                    #endregion
                }
            }
            KismetHelper.CreateOutputLink(LastMarkRead, "Out", MarkReadOutLink);
            KismetHelper.CreateOutputLink(LastDisplayMessage, "Out", DisplayMessageOutLink);
            ArchiveSwitch.WriteProperty(new IntProperty(currentSwCount, "LinkCount"));

            stateEventMapExport.WriteBinary(StateEventMap);

            // Save Messages file into DLC
            var cookedDir = Path.Combine(M3Directories.GetDLCPath(target), M3MergeDLC.MERGE_DLC_FOLDERNAME, target.Game.CookedDirName());
            var outMessages = Path.Combine(cookedDir, @"BioD_Nor103_Messages.pcc");
            pcc.Save(outMessages);

            // Save Startup file into DLC
            var startupF = Path.Combine(cookedDir, $@"Startup_{M3MergeDLC.MERGE_DLC_FOLDERNAME}.pcc");
            startup.Save(startupF);
        }
    }
}
