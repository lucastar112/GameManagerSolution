using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace DebugManager.Services;

public sealed class RaceAssetService
{
    private static readonly Dictionary<byte, string> CharacterAssets = new()
    {
        // 00 - 17
        [0x00] = "Mario.png",
        [0x01] = "Starlet.png",
        [0x02] = "Waluigi.png",
        [0x03] = "Bowser.png",
        [0x04] = "BowserJr.png",
        [0x05] = "DryBones.png",
        [0x06] = "Cyber.png",
        [0x07] = "MrL.png",
        [0x08] = "Plankton.png",
        [0x09] = "Homer.png",
        [0x0A] = "Yoshi.png",
        [0x0B] = "Patrick.png",
        [0x0C] = "Hero.png",
        [0x0D] = "Toadette.png",
        [0x0E] = "DarkBowser.png",
        [0x0F] = "Daisy.png",
        [0x10] = "Peach.png",
        [0x11] = "Birdo.png",
        [0x12] = "Diddy.png",
        [0x13] = "Boo.png",
        [0x14] = "Spongebob.png",
        [0x15] = "SkeletteBowser.png",
        [0x16] = "Trump.png",
        [0x17] = "Rosalina.png",

        // Mii characters
        [0x18] = "tt_mii_suit_a_64x64.tpl.png",
        [0x19] = "tt_mii_suit_a_64x64.tpl.png",
        [0x1A] = "tt_mii_suit_b_64x64.tpl.png",
        [0x1B] = "tt_mii_suit_b_64x64.tpl.png",
        [0x1C] = "tt_mii_suit_c_64x64.tpl.png",
        [0x1D] = "tt_mii_suit_c_64x64.tpl.png",

        [0x1E] = "tt_mii_suit_a_64x64.tpl.png",
        [0x1F] = "tt_mii_suit_a_64x64.tpl.png",
        [0x20] = "tt_mii_suit_b_64x64.tpl.png",
        [0x21] = "tt_mii_suit_b_64x64.tpl.png",
        [0x22] = "tt_mii_suit_c_64x64.tpl.png",
        [0x23] = "tt_mii_suit_c_64x64.tpl.png",

        [0x24] = "tt_mii_suit_a_64x64.tpl.png",
        [0x25] = "tt_mii_suit_a_64x64.tpl.png",
        [0x26] = "tt_mii_suit_b_64x64.tpl.png",
        [0x27] = "tt_mii_suit_b_64x64.tpl.png",
        [0x28] = "tt_mii_suit_c_64x64.tpl.png",
        [0x29] = "tt_mii_suit_c_64x64.tpl.png",

        [0x2A] = "tt_mii_suit_a_64x64.tpl.png",
        [0x2B] = "tt_mii_suit_a_64x64.tpl.png",
        [0x2C] = "tt_mii_suit_a_64x64.tpl.png"
    };

    private static readonly Dictionary<byte, string> VehicleAssets = new()
    {
        [0x00] = "StandardKartS.png",
        [0x01] = "StandardKartM.png",
        [0x02] = "StandardKartL.png",
        [0x03] = "BoosterSeat.png",
        [0x04] = "ClassicDragster.png",
        [0x05] = "Offroader.png",
        [0x06] = "MiniBeast.png",
        [0x07] = "WildWing.png",
        [0x08] = "FlameFlyer.png",
        [0x09] = "CheepCharger.png",
        [0x0A] = "SuperBlooper.png",
        [0x0B] = "PiranhaProwler.png",
        [0x0C] = "TinyTitan.png",
        [0x0D] = "Daytripper.png",
        [0x0E] = "Jetsetter.png",
        [0x0F] = "BlueFalcon.png",
        [0x10] = "Sprinter.png",
        [0x11] = "Honeycoupe.png",
        [0x12] = "StandardBikeS.png",
        [0x13] = "StandardBikeM.png",
        [0x14] = "StandardBikeL.png",
        [0x15] = "BulletBike.png",
        [0x16] = "MachBike.png",
        [0x17] = "FlameRunner.png",
        [0x18] = "BitBike.png",
        [0x19] = "Sugarscoot.png",
        [0x1A] = "WarioBike.png",
        [0x1B] = "Quacker.png",
        [0x1C] = "ZipZip.png",
        [0x1D] = "ShootingStar.png",
        [0x1E] = "Magikruiser.png",
        [0x1F] = "Sneakster.png",
        [0x20] = "Spear.png",
        [0x21] = "Bubble.png",
        [0x22] = "DolphinDasher.png",
        [0x23] = "Phantom.png"
    };

    public BitmapImage GetCharacterImage(byte characterId)
    {
        if (!CharacterAssets.TryGetValue(characterId, out string? fileName))
            return LoadImage("Characters/Unknown.png");

        return LoadImage($"Characters/{fileName}");
    }

    public BitmapImage GetVehicleImage(byte vehicleId)
    {
        if (!VehicleAssets.TryGetValue(vehicleId, out string? fileName))
            return LoadImage("Vehicles/Unknown.png");

        return LoadImage($"Vehicles/{fileName}");
    }

    private static BitmapImage LoadImage(string path)
    {
        return new BitmapImage(
            new Uri(
                $"pack://application:,,,/Assets/{path}",
                UriKind.Absolute));
    }
}
