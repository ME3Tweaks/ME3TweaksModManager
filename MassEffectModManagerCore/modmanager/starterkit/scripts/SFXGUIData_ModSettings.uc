Class SFXGUIData_ModSettings extends SFXGameChoiceGUIData
    native
    editinlinenew
    perobjectconfig
    config(UI);

// Types
struct native PlotIntSetting 
{
    var int PlotIntValue;
    var int PlotIntId;
};
struct native ModSettingItemData 
{
    var SFXChoiceEntry ChoiceEntry;
    var array<PlotIntSetting> ApplySettingInts;
    var array<int> ApplySettingBools;
    var string SubMenuClassName;
    var int DisplayConditionalID;
    var int DisplayPlotBool;
    var int EnableConditional;
    var int EnablePlotBool;
    var unkflag1 string Image;
    var stringref ConfirmationMessageOverride;
    var stringref ConfirmationMessageATextOverride;
    var bool skipConfirmationDialog;
    var bool exitSubmenuOnApply;
    var string ConfirmationDialogMessage;
    
    structdefaultproperties
    {
        ApplySettingInts = ()
        ApplySettingBools = ()
    }
};

// Variables
var config string DefaultImage;
var config stringref srStoreDescription;
var config stringref srOutOfStockDescription;
var config stringref srOutOfStock;
var config stringref ConfirmationMessageATextOverride;
var config array<ModSettingItemData> ModSettingItemArray;

// Functions
public function bool HandleItemConfirmed(ModSettingItemData item, int index)
{
    return FALSE;
}
public function bool HandleItemSelected(ModSettingItemData item, int index)
{
    return FALSE;
}
public function bool HandleMenuSetup()
{
    return FALSE;
}

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{
}