﻿#region License

// Copyright (c) 2005-2014, CellAO Team
// 
// 
// All rights reserved.
// 
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
// EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
// PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

#endregion

namespace ZoneEngine.Core.PacketHandlers
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using CellAO.Core.Items;
    using CellAO.Core.Network;
    using CellAO.Enums;

    using ZoneEngine.Core.MessageHandlers;
    using ZoneEngine.Core.Packets;

    #endregion

    /// <summary>
    /// </summary>
    public static class TradeSkillReceiver
    {
        #region Static Fields

        /// <summary>
        /// </summary>
        private static readonly List<TradeSkillInfo> TradeSkillInfos = new List<TradeSkillInfo>();

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// </summary>
        /// <param name="sourceItem">
        /// </param>
        /// <param name="targetItem">
        /// </param>
        /// <param name="newItem">
        /// </param>
        /// <returns>
        /// </returns>
        public static string SuccessMessage(Item sourceItem, Item targetItem, Item newItem)
        {
            return string.Format(
                "You combined \"{0}\" with \"{1}\" and the result is a quality level {2} \"{3}\".",
                TradeSkill.Instance.GetItemName(sourceItem.LowID, sourceItem.HighID, sourceItem.Quality),
                TradeSkill.Instance.GetItemName(targetItem.LowID, targetItem.HighID, targetItem.Quality),
                newItem.Quality,
                TradeSkill.Instance.GetItemName(newItem.LowID, newItem.HighID, newItem.Quality));
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <param name="quality">
        /// </param>
        public static void TradeSkillBuildPressed(IZoneClient client, int quality)
        {
            TradeSkillInfo source = client.Controller.Character.TradeSkillSource;
            TradeSkillInfo target = client.Controller.Character.TradeSkillTarget;

            Item sourceItem = client.Controller.Character.BaseInventory.GetItemInContainer(
                source.Container,
                source.Placement);
            Item targetItem = client.Controller.Character.BaseInventory.GetItemInContainer(
                target.Container,
                target.Placement);

            TradeSkillEntry ts = TradeSkill.Instance.GetTradeSkillEntry(sourceItem.HighID, targetItem.HighID);

            if (ts != null)
            {
                quality = Math.Min(quality, ItemLoader.ItemList[ts.ResultHighId].Quality);
                if (WindowBuild(client, quality, ts, sourceItem, targetItem))
                {
                    Item newItem = new Item(quality, ts.ResultLowId, ts.ResultHighId);
                    InventoryError inventoryError = client.Controller.Character.BaseInventory.TryAdd(newItem);
                    if (inventoryError == InventoryError.OK)
                    {
                        AddTemplateMessageHandler.Default.Send(client.Controller.Character, newItem);

                        // Delete source?
                        if ((ts.DeleteFlag & 1) == 1)
                        {
                            client.Controller.Character.BaseInventory.RemoveItem(source.Container, source.Placement);
                            CharacterActionMessageHandler.Default.SendDeleteItem(
                                client.Controller.Character,
                                source.Container,
                                source.Placement);
                        }

                        // Delete target?
                        if ((ts.DeleteFlag & 2) == 2)
                        {
                            client.Controller.Character.BaseInventory.RemoveItem(target.Container, target.Placement);
                            CharacterActionMessageHandler.Default.SendDeleteItem(
                                client.Controller.Character,
                                target.Container,
                                target.Placement);
                        }

                        ChatTextMessageHandler.Default.Send(
                            client.Controller.Character,
                            SuccessMessage(sourceItem, targetItem, new Item(quality, ts.ResultLowId, ts.ResultHighId)));

                        client.Controller.Character.Stats[StatIds.xp].Value += CalculateXP(quality, ts);
                    }
                }
            }
            else
            {
                ChatTextMessageHandler.Default.Send(
                    client.Controller.Character,
                    "It is not possible to assemble those two items. Maybe the order was wrong?");
                ChatTextMessageHandler.Default.Send(client.Controller.Character, "No combination found!");
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <param name="container">
        /// </param>
        /// <param name="placement">
        /// </param>
        public static void TradeSkillSourceChanged(IZoneClient client, int container, int placement)
        {
            if ((container != 0) && (placement != 0))
            {
                client.Controller.Character.TradeSkillSource = new TradeSkillInfo(0, container, placement);

                Item item = client.Controller.Character.BaseInventory.GetItemInContainer(container, placement);
                TradeSkillPacket.SendSource(
                    client.Controller.Character,
                    TradeSkill.Instance.SourceProcessesCount(item.HighID));

                TradeSkillChanged(client);
            }
            else
            {
                client.Controller.Character.TradeSkillSource = null;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <param name="container">
        /// </param>
        /// <param name="placement">
        /// </param>
        public static void TradeSkillTargetChanged(IZoneClient client, int container, int placement)
        {
            if ((container != 0) && (placement != 0))
            {
                client.Controller.Character.TradeSkillTarget = new TradeSkillInfo(0, container, placement);

                Item item = client.Controller.Character.BaseInventory.GetItemInContainer(container, placement);
                TradeSkillPacket.SendTarget(
                    client.Controller.Character,
                    TradeSkill.Instance.TargetProcessesCount(item.HighID));

                TradeSkillChanged(client);
            }
            else
            {
                client.Controller.Character.TradeSkillTarget = null;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// </summary>
        /// <param name="quality">
        /// </param>
        /// <param name="ts">
        /// </param>
        /// <returns>
        /// </returns>
        private static int CalculateXP(int quality, TradeSkillEntry ts)
        {
            int absMinQL = ItemLoader.ItemList[ts.ResultLowId].Quality;
            int absMaxQL = ItemLoader.ItemList[ts.ResultHighId].Quality;
            if (absMaxQL == absMinQL)
            {
                return ts.MaxXP;
            }

            return
                (int)
                    Math.Floor(
                        (double)((ts.MaxXP - ts.MinXP) / (absMaxQL - absMinQL)) * (quality - absMinQL) + ts.MinXP);
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        private static void TradeSkillChanged(IZoneClient client)
        {
            TradeSkillInfo source = client.Controller.Character.TradeSkillSource;
            TradeSkillInfo target = client.Controller.Character.TradeSkillTarget;

            if ((source != null) && (target != null))
            {
                Item sourceItem = client.Controller.Character.BaseInventory.GetItemInContainer(
                    source.Container,
                    source.Placement);
                Item targetItem = client.Controller.Character.BaseInventory.GetItemInContainer(
                    target.Container,
                    target.Placement);

                TradeSkillEntry ts = TradeSkill.Instance.GetTradeSkillEntry(sourceItem.HighID, targetItem.HighID);
                if (ts != null)
                {
                    if (ts.ValidateRange(sourceItem.Quality, targetItem.Quality))
                    {
                        foreach (TradeSkillSkill tsi in ts.Skills)
                        {
                            int skillReq = (int)Math.Ceiling(tsi.Percent / 100M * targetItem.Quality);
                            if (skillReq > client.Controller.Character.Stats[tsi.StatId].Value)
                            {
                                TradeSkillPacket.SendRequirement(client.Controller.Character, tsi.StatId, skillReq);
                            }
                        }

                        int leastbump = 0;
                        int maxbump = 0;
                        if (ts.IsImplant)
                        {
                            if (targetItem.Quality >= 250)
                            {
                                maxbump = 5;
                            }
                            else if (targetItem.Quality >= 201)
                            {
                                maxbump = 4;
                            }
                            else if (targetItem.Quality >= 150)
                            {
                                maxbump = 3;
                            }
                            else if (targetItem.Quality >= 100)
                            {
                                maxbump = 2;
                            }
                            else if (targetItem.Quality >= 50)
                            {
                                maxbump = 1;
                            }
                        }

                        foreach (TradeSkillSkill tsSkill in ts.Skills)
                        {
                            if (tsSkill.SkillPerBump != 0)
                            {
                                leastbump =
                                    Math.Min(
                                        Convert.ToInt32(
                                            (client.Controller.Character.Stats[tsSkill.StatId].Value
                                             - (tsSkill.Percent / 100M * targetItem.Quality)) / tsSkill.SkillPerBump),
                                        maxbump);
                            }
                        }

                        TradeSkillPacket.SendResult(
                            client.Controller.Character,
                            targetItem.Quality,
                            Math.Min(targetItem.Quality + leastbump, ItemLoader.ItemList[ts.ResultHighId].Quality),
                            ts.ResultLowId,
                            ts.ResultHighId);
                    }
                    else
                    {
                        TradeSkillPacket.SendOutOfRange(
                            client.Controller.Character,
                            Convert.ToInt32(
                                Math.Round((double)targetItem.Quality - ts.QLRangePercent * targetItem.Quality / 100)));
                    }
                }
                else
                {
                    TradeSkillPacket.SendNotTradeskill(client.Controller.Character);
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="client">
        /// </param>
        /// <param name="desiredQuality">
        /// </param>
        /// <param name="ts">
        /// </param>
        /// <param name="sourceItem">
        /// </param>
        /// <param name="targetItem">
        /// </param>
        /// <returns>
        /// </returns>
        private static bool WindowBuild(
            IZoneClient client,
            int desiredQuality,
            TradeSkillEntry ts,
            Item sourceItem,
            Item targetItem)
        {
            if (!((ts.MinTargetQL >= targetItem.Quality) || (ts.MinTargetQL == 0)))
            {
                return false;
            }

            if (!ts.ValidateRange(sourceItem.Quality, targetItem.Quality))
            {
                return false;
            }

            foreach (TradeSkillSkill tss in ts.Skills)
            {
                if (client.Controller.Character.Stats[tss.StatId].Value
                    < Convert.ToInt32(tss.Percent / 100M * targetItem.Quality))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}