namespace EchoProtocol.Api.Common;

public static class ShopItemCategories
{
    public const string Character = "CHARACTER";
    public const string TeamTool = "TEAM_TOOL";
    public const string Pet = "PET";
    public const string PetSkin = "PET_SKIN";

    public static bool IsSupported(string category) =>
        category is Character
            or TeamTool
            or Pet
            or PetSkin;
}
