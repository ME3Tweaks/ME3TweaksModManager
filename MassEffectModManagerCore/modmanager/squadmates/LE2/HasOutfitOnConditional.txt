public function bool F%CONDITIONALNUM%(BioWorldInfo bioWorld, int Argument)
{
    local BioGlobalVariableTable gv;
    
    gv = bioWorld.GetGlobalVariables();
    return gv.GetInt(%SQUADMATEOUTFITPLOTINT%) == %OUTFITINDEX%;
}