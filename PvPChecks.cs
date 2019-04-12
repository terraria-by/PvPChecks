using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace PvPChecks
{
    [ApiVersion(2, 1)]
    public class PvPChecks : TerrariaPlugin
    {
        private string configPath = Path.Combine(TShock.SavePath, "pvpchecks.json");
        private Config cfg;

        public override string Name => "PvPChecks";
        public override string Author => "Johuan & Veelnyr";
        public override string Description => "Bans weapons, buffs, accessories, projectiles and disables PvPers from using illegitimate stuff.";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;
        public PvPChecks(Main game) : base(game) { }

        public override void Initialize()
        {
            cfg = Config.ReadOrCreate(configPath);
            GetDataHandlers.PlayerUpdate += OnPlayerUpdate;
            GetDataHandlers.NewProjectile += OnNewProjectile;

            Commands.ChatCommands.Add(new Command(PvPWeaponBans, "pvpitembans"));
            Commands.ChatCommands.Add(new Command(PvPBuffBans, "pvpbuffbans"));
            Commands.ChatCommands.Add(new Command(PvPProjBans, "pvpprojbans"));
            Commands.ChatCommands.Add(new Command("pvpchecks.ban", BanItem, "banitem"));
            Commands.ChatCommands.Add(new Command("pvpchecks.ban", BanBuff, "banbuff"));
            Commands.ChatCommands.Add(new Command("pvpchecks.ban", BanProj, "banproj"));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GetDataHandlers.PlayerUpdate -= OnPlayerUpdate;
                GetDataHandlers.NewProjectile -= OnNewProjectile;
            }
            base.Dispose(disposing);
        }

        DateTime[] WarningMsgCooldown = new DateTime[256];
        private void OnPlayerUpdate(object sender, GetDataHandlers.PlayerUpdateEventArgs args)
        {
            TSPlayer player = TShock.Players[args.PlayerId];

            //If the player isn't in pvp or using an item, skip pvp checking
            if (!player.TPlayer.hostile || (args.Control & 32) == 0) return;
            if (player.HasPermission("pvpchecks.ignore")) return;

            //Check weapon
            foreach (int weapon in cfg.weaponBans)
            {
                if (player.SelectedItem.type == weapon || player.ItemInHand.type == weapon)
                {
                    player.Disable("Used banned weapon in pvp.", DisableFlags.None);
                    if ((DateTime.Now - WarningMsgCooldown[player.Index]).TotalSeconds > 3)
                    {
                        player.SendErrorMessage("[i:{0}] {1} cannot be used in PvP. See /pvpitembans.", weapon, TShock.Utils.GetItemById(weapon).Name);
                        WarningMsgCooldown[player.Index] = DateTime.Now;
                    }
                    return;
                }
            }

            //Check armor
            for (int a = 0; a < 3; a++)
            {
                foreach (int armorBan in cfg.armorBans)
                {
                    if (player.TPlayer.armor[a].type == armorBan)
                    {
                        player.Disable("Used banned armor in pvp.", DisableFlags.None);
                        Console.WriteLine("47: " + player.TPlayer.buffType.Contains(47));
                        if ((DateTime.Now - WarningMsgCooldown[player.Index]).TotalSeconds > 3)
                        {
                            player.SendErrorMessage("[i:{0}] {1} cannot be used in PvP. See /pvpitembans.", armorBan, TShock.Utils.GetItemById(armorBan).Name);
                            WarningMsgCooldown[player.Index] = DateTime.Now;
                        }
                        return;
                    }
                }
            }

            //Check accs
            for (int a = 3; a < 9; a++)
            {
                foreach (int accBan in cfg.accsBans)
                {
                    if (player.TPlayer.armor[a].type == accBan)
                    {
                        player.Disable("Used banned accessory in pvp.", DisableFlags.None);
                        if ((DateTime.Now - WarningMsgCooldown[player.Index]).TotalSeconds > 3)
                        {
                            player.SendErrorMessage("[i:{0}] {1} cannot be used in PvP. See /pvpitembans.", accBan, TShock.Utils.GetItemById(accBan).Name);
                            WarningMsgCooldown[player.Index] = DateTime.Now;
                        }
                        return;
                    }
                }
            }

            //Checks buffs
            foreach (int buff in cfg.buffBans)
            {
                foreach (int playerbuff in player.TPlayer.buffType)
                {
                    if (playerbuff == buff)
                    {
                        player.Disable("Used banned buff.", DisableFlags.None);
                        if ((DateTime.Now - WarningMsgCooldown[player.Index]).TotalSeconds > 3)
                        {
                            player.SendErrorMessage(TShock.Utils.GetBuffName(playerbuff) + " cannot be used in PvP. See /pvpbuffbans.");
                            WarningMsgCooldown[player.Index] = DateTime.Now;
                        }
                        return;
                    }
                }
            }

            //Checks whether a player is wearing duplicate accessories/armor
            List<int> duplicate = new List<int>();
            foreach (Item equip in player.TPlayer.armor)
            {
                if (duplicate.Contains(equip.type))
                {
                    player.Disable("Used duplicate accessories.", DisableFlags.None);
                    player.SendErrorMessage("Please remove the duplicate accessory for PvP: " + equip.Name);
                    return;
                }
                else if (equip.type != 0)
                {
                    duplicate.Add(equip.type);
                }
            }

            //Checks whether the player is using the unobtainable 7th accessory slot
            if (player.TPlayer.armor[9].netID != 0)
            {
                player.Disable("Used 7th accessory slot.", DisableFlags.None);
                player.SendErrorMessage("The 7th accessory slot cannot be used in PvP.");
            }
        }

        private void OnNewProjectile(object sender, GetDataHandlers.NewProjectileEventArgs args)
        {
            TSPlayer player = TShock.Players[args.Owner];

            if (!player.TPlayer.hostile) return;
            if (player.HasPermission("pvpchecks.ignore")) return;

            if (cfg.projBans.Contains(args.Type))
            {
                player.Disable("Used banned projectile in pvp.", DisableFlags.None);
                player.SendErrorMessage("You cannot create this projectile in PvP. See /pvpprojbans.");
            }
        }

        private void BanItem(CommandArgs args)
        {
            TSPlayer plr = args.Player;

            if (args.Parameters.Count != 2)
            {
                plr.SendErrorMessage("Usage: /banitem <add/del> <item name/ID>");
                return;
            }

            switch (args.Parameters[0].ToLower())
            {
                case "add":
                    List<Item> foundAddItems = TShock.Utils.GetItemByIdOrName(args.Parameters[1]).Where(i => i.ammo == 0).ToList();

                    if (foundAddItems.Count == 1)
                    {
                        Item i = foundAddItems[0];

                        if (i.accessory)
                        {
                            if (!cfg.accsBans.Contains(i.type))
                            {
                                cfg.accsBans.Add(i.type);
                            }
                        }
                        else if (i.headSlot >= 0 || i.bodySlot >= 0 || i.legSlot >= 0) //armor
                        {
                            if (!cfg.armorBans.Contains(i.type))
                            {
                                cfg.armorBans.Add(i.type);
                            }
                        }
                        else if (i.damage > 0) //weapon
                        {
                            if (!cfg.weaponBans.Contains(i.type))
                            {
                                cfg.weaponBans.Add(i.type);
                            }
                        }
                        else
                        {
                            plr.SendErrorMessage("No items found by that name/ID.");
                            break;
                        }
                        cfg.Write(configPath);
                        args.Player.SendSuccessMessage("Banned {0} in pvp.", i.Name);
                    }
                    else if (foundAddItems.Count == 0)
                    {
                        plr.SendErrorMessage("No items found by that name/ID.");
                    }
                    else
                    {
                        IEnumerable<string> itemNames = from foundItem in foundAddItems
                                                        select TShock.Utils.GetItemById(foundItem.type).Name;
                        PaginationTools.SendPage(plr, 0, PaginationTools.BuildLinesFromTerms(itemNames),
                            new PaginationTools.Settings
                            {
                                HeaderTextColor = Color.Red,
                                IncludeFooter = false,
                                HeaderFormat = "More than one item found:"
                            });
                    }
                    break;

                case "del":
                    List<Item> foundDelItems = TShock.Utils.GetItemByIdOrName(args.Parameters[1]).Where(i => (cfg.weaponBans.Contains(i.type) || cfg.accsBans.Contains(i.type) || cfg.armorBans.Contains(i.type)) && i.ammo == 0).ToList();

                    if (foundDelItems.Count == 1)
                    {
                        Item i = foundDelItems[0];

                        if (cfg.weaponBans.Remove(i.type) || cfg.accsBans.Remove(i.type) || cfg.armorBans.Remove(i.type))
                        {
                            cfg.Write(configPath);
                            args.Player.SendSuccessMessage("Unbanned {0} in pvp.", i.Name);
                        }
                    }
                    else if (foundDelItems.Count == 0)
                    {
                        plr.SendErrorMessage("No items found by that name/ID in ban list.");
                    }
                    else
                    {
                        IEnumerable<string> itemNames = from foundItem in foundDelItems
                                                        select TShock.Utils.GetItemById(foundItem.type).Name;
                        PaginationTools.SendPage(plr, 0, PaginationTools.BuildLinesFromTerms(itemNames),
                            new PaginationTools.Settings
                            {
                                HeaderTextColor = Color.Red,
                                IncludeFooter = false,
                                HeaderFormat = "More than one item found:"
                            });
                    }
                    break;

                default:
                    plr.SendErrorMessage("Invalid syntax! /banitem <add/del> <item name/ID>");
                    break;
            }
        }

        private void BanBuff(CommandArgs args)
        {
            TSPlayer plr = args.Player;

            if (args.Parameters.Count != 2)
            {
                plr.SendErrorMessage("Usage: /banbuff <add/del> <buff name/ID>");
                return;
            }

            switch (args.Parameters[0].ToLower())
            {
                case "add":
                    int addid;
                    if (!int.TryParse(args.Parameters[1], out addid))
                    {
                        var found = TShock.Utils.GetBuffByName(args.Parameters[1]);
                        if (found.Count == 0)
                        {
                            plr.SendErrorMessage("No buffs found by that name/ID.");
                            return;
                        }
                        else if (found.Count > 1)
                        {
                            IEnumerable<string> buffNames = from foundBuff in found select TShock.Utils.GetBuffName(foundBuff);
                            PaginationTools.SendPage(plr, 0, PaginationTools.BuildLinesFromTerms(buffNames),
                                new PaginationTools.Settings
                                {
                                    HeaderTextColor = Color.Red,
                                    IncludeFooter = false,
                                    HeaderFormat = "More than one buff found:"
                                });
                            return;
                        }
                        addid = found[0];
                    }

                    if (addid > 0 && addid < Main.maxBuffTypes)
                    {
                        if (!cfg.buffBans.Contains(addid))
                        {
                            cfg.buffBans.Add(addid);
                            cfg.Write(configPath);
                        }
                        args.Player.SendSuccessMessage("Banned {0} in pvp.", Lang.GetBuffName(addid));
                    }
                    else
                    {
                        plr.SendErrorMessage("Invalid buff ID.");
                    }
                    break;

                case "del":
                    int delid;
                    if (!int.TryParse(args.Parameters[1], out delid))
                    {
                        var found = TShock.Utils.GetBuffByName(args.Parameters[1]).Where(b => cfg.buffBans.Contains(b)).ToList();
                        if (found.Count == 0)
                        {
                            plr.SendErrorMessage("No buffs found by that name/ID in ban list.");
                            return;
                        }
                        else if (found.Count > 1)
                        {
                            IEnumerable<string> buffNames = from foundBuff in found select TShock.Utils.GetBuffName(foundBuff);
                            PaginationTools.SendPage(plr, 0, PaginationTools.BuildLinesFromTerms(buffNames),
                                new PaginationTools.Settings
                                {
                                    HeaderTextColor = Color.Red,
                                    IncludeFooter = false,
                                    HeaderFormat = "More than one buff found:"
                                });
                            return;
                        }
                        delid = found[0];
                    }

                    if (delid > 0 && delid < Main.maxBuffTypes)
                    {
                        if (cfg.buffBans.Contains(delid))
                        {
                            cfg.buffBans.Remove(delid);
                            cfg.Write(configPath);
                            args.Player.SendSuccessMessage("Unbanned {0} in pvp.", Lang.GetBuffName(delid));
                            break;
                        }
                        plr.SendErrorMessage("No buffs found by that name/ID in ban list.");
                    }
                    else
                    {
                        plr.SendErrorMessage("Invalid buff ID.");
                    }
                    break;

                default:
                    plr.SendErrorMessage("Invalid syntax! /banbuff <add/del> <buff name/ID>");
                    break;
            }
        }

        private void BanProj(CommandArgs args)
        {
            TSPlayer plr = args.Player;

            if (args.Parameters.Count != 2)
            {
                plr.SendErrorMessage("Usage: /banproj <add/del> <projectile ID>");
                return;
            }

            switch (args.Parameters[0].ToLower())
            {
                case "add":
                    int addid;
                    if (int.TryParse(args.Parameters[1], out addid) && addid > 0 && addid <= 713)
                    {
                        if (!cfg.projBans.Contains(addid))
                        {
                            cfg.projBans.Add(addid);
                            cfg.Write(configPath);
                        }
                        args.Player.SendSuccessMessage("Banned projectile {0} in pvp.", addid);
                        break;
                    }
                    plr.SendErrorMessage("Invalid projectile ID.");
                    break;

                case "del":
                    int delid;
                    if (int.TryParse(args.Parameters[1], out delid) && delid > 0 && delid <= 713)
                    {
                        if (cfg.projBans.Contains(delid))
                        {
                            cfg.projBans.Remove(delid);
                            cfg.Write(configPath);
                        }
                        args.Player.SendSuccessMessage("Unbanned projectile {0} in pvp.", delid);
                        break;
                    }
                    plr.SendErrorMessage("Invalid projectile ID.");
                    break;

                default:
                    plr.SendErrorMessage("Invalid syntax! /banproj <add/del> <projectile ID>");
                    break;
            }
        }

        private void PvPWeaponBans(CommandArgs args)
        {
            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 0, args.Player, out pageNumber))
                return;
            IEnumerable<string> weaponNames = from weaponBan in cfg.weaponBans
                                              select TShock.Utils.GetItemById(weaponBan).Name;
            PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(weaponNames),
                new PaginationTools.Settings
                {
                    HeaderFormat = "The following weapons cannot be used in PvP:",
                    FooterFormat = "Type /pvpweaponbans {{0}} for more.",
                    NothingToDisplayString = "There are currently no banned weapons."
                });
        }
        private void PvPBuffBans(CommandArgs args)
        {
            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 0, args.Player, out pageNumber))
                return;
            IEnumerable<string> buffNames = from buffBan in cfg.buffBans
                                            select TShock.Utils.GetItemById(buffBan).Name;
            PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(buffNames),
                new PaginationTools.Settings
                {
                    HeaderFormat = "The following buffs cannot be used in PvP:",
                    FooterFormat = "Type /pvpbuffbans {{0}} for more.",
                    NothingToDisplayString = "There are currently no banned buffs."
                });
        }
        private void PvPProjBans(CommandArgs args)
        {
            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 0, args.Player, out pageNumber))
                return;
            PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(cfg.projBans),
                new PaginationTools.Settings
                {
                    HeaderFormat = "The following projectiles cannot be used in PvP:",
                    FooterFormat = "Type /pvpprojbans {{0}} for more.",
                    NothingToDisplayString = "There are currently no banned projectiles."
                });
        }
    }
}
