﻿using AntShares.Core;
using AntShares.IO.Json;
using AntShares.Wallets;
using System.Collections.Generic;
using System.Linq;

namespace AntShares.Network.RPC
{
    internal class RpcServerWithWallet : RpcServer
    {
        public RpcServerWithWallet(LocalNode localNode)
            : base(localNode)
        {
        }

        protected override JObject Process(string method, JArray _params)
        {
            switch (method)
            {
                case "getbalance":
                    if (Program.Wallet == null)
                        throw new RpcException(-400, "Access denied.");
                    else
                    {
                        UInt256 assetId = UInt256.Parse(_params[0].AsString());
                        IEnumerable<Coin> coins = Program.Wallet.GetCoins().Where(p => !p.State.HasFlag(CoinState.Spent) && p.Output.AssetId.Equals(assetId));
                        JObject json = new JObject();
                        json["balance"] = coins.Sum(p => p.Output.Value).ToString();
                        json["confirmed"] = coins.Where(p => p.State.HasFlag(CoinState.Confirmed)).Sum(p => p.Output.Value).ToString();
                        return json;
                    }
                case "sendtoaddress":
                    if (Program.Wallet == null)
                        throw new RpcException(-400, "Access denied");
                    else
                    {
                        UInt256 assetId = UInt256.Parse(_params[0].AsString());
                        UInt160 scriptHash = Wallet.ToScriptHash(_params[1].AsString());
                        Fixed8 value = Fixed8.Parse(_params[2].AsString());
                        Fixed8 fee = _params.Count >= 4 ? Fixed8.Parse(_params[3].AsString()) : Fixed8.Zero;
                        if (value <= Fixed8.Zero)
                            throw new RpcException(-32602, "Invalid params");
                        ContractTransaction tx = Program.Wallet.MakeTransaction(new ContractTransaction
                        {
                            Outputs = new[]
                            {
                                new TransactionOutput
                                {
                                    AssetId = assetId,
                                    Value = value,
                                    ScriptHash = scriptHash
                                }
                            }
                        }, fee: fee);
                        if (tx == null)
                            throw new RpcException(-300, "Insufficient funds");
                        SignatureContext context = new SignatureContext(tx);
                        Program.Wallet.Sign(context);
                        if (context.Completed)
                        {
                            tx.Scripts = context.GetScripts();
                            Program.Wallet.SaveTransaction(tx);
                            LocalNode.Relay(tx);
                            return tx.ToJson();
                        }
                        else
                        {
                            return context.ToJson();
                        }
                    }
                default:
                    return base.Process(method, _params);
            }
        }
    }
}
