using System.Diagnostics;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Miningcore.Blockchain.Bitcoin;

public static class BitcoinUtils
{
    /// <summary>
    /// Bitcoin addresses are implemented using the Base58Check encoding of the hash of either:
    /// Pay-to-script-hash(p2sh): payload is: RIPEMD160(SHA256(redeemScript)) where redeemScript is a
    /// script the wallet knows how to spend; version byte = 0x05 (these addresses begin with the digit '3')
    /// Pay-to-pubkey-hash(p2pkh): payload is RIPEMD160(SHA256(ECDSA_publicKey)) where
    /// ECDSA_publicKey is a public key the wallet knows the private key for; version byte = 0x00
    /// (these addresses begin with the digit '1')
    /// The resulting hash in both of these cases is always exactly 20 bytes.
    /// </summary>
    public static IDestination AddressToDestination(string address, Network expectedNetwork)
    {
        var decoded = Encoders.Base58Check.DecodeData(address);
        var networkVersionBytes = expectedNetwork.GetVersionBytes(Base58Type.PUBKEY_ADDRESS, true);
        decoded = decoded.Skip(networkVersionBytes.Length).ToArray();
        var result = new KeyId(decoded);

        return result;
    }

    public static IDestination BechSegwitAddressToDestination(string address, Network expectedNetwork)
    {
        var encoder = expectedNetwork.GetBech32Encoder(Bech32Type.WITNESS_PUBKEY_ADDRESS, true);
        var decoded = encoder.Decode(address, out var witVersion);
        var result = new WitKeyId(decoded);

        Debug.Assert(result.GetAddress(expectedNetwork).ToString() == address);
        return result;
    }

    public static IDestination BCashAddressToDestination(string address, Network expectedNetwork)
    {
        // Map the expectedNetwork's ChainName to the BCash library's ChainName
        ChainName chainName = expectedNetwork.ChainName.ToString().ToLower() switch
        {
            "mainnet" or "main"     => ChainName.Mainnet,
            "testnet4" or "test4"     => ChainName.Testnet,
            "regtest" or "reg"        => ChainName.Regtest,
            _ => throw new ArgumentException("Unknown network chain name", nameof(expectedNetwork))
        };

        // Get the appropriate Bitcoin Cash network instance
        var bcashNetwork = NBitcoin.Altcoins.BCash.Instance.GetNetwork(chainName);

        // If the address doesn't contain a colon, assume it's missing the CashAddr prefix.
        if (!address.Contains(":"))
        {
            if (chainName == ChainName.Mainnet)
                address = "bitcoincash:" + address;
            else if (chainName == ChainName.Testnet)
                address = "bchtest:" + address;
            else if (chainName == ChainName.Regtest)
                address = "bchreg:" + address;
        }

        var pubKeyAddress = bcashNetwork.Parse<NBitcoin.Altcoins.BCash.BTrashPubKeyAddress>(address);
        return pubKeyAddress.ScriptPubKey.GetDestinationAddress(bcashNetwork);
    }

    public static IDestination DgbAddressToDestination(string address, Network network)
    {
        // Build a custom DigiByte network using NetworkBuilder.
            // We set only the parameters required for address encoding/decoding.
            // Note: For parsing, dummy values for consensus (e.g. genesis hash) and magic are acceptable.
            var digiByteNetwork = new NetworkBuilder()
                .SetName($"Digibyte-{network.Name}")
                .SetConsensus(new Consensus
                {
                    SubsidyHalvingInterval = 840000,
                    MajorityEnforceBlockUpgrade = 750,
                    MajorityRejectBlockOutdated = 950,
                    MajorityWindow = 1000,
                    PowLimit = new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")),
                    PowTargetTimespan = TimeSpan.FromMinutes(1),
                    PowTargetSpacing = TimeSpan.FromMinutes(1),
                    PowAllowMinDifficultyBlocks = false,
                    PowNoRetargeting = false,
                    RuleChangeActivationThreshold = 1916,
                    MinerConfirmationWindow = 2016
                })
                // Dummy magic value – only matters for p2p, not for address parsing.
                .SetMagic(0xF9BEB4D9)
                .SetPort(12024)
                .SetRPCPort(14022)
                // Set DigiByte legacy (Base58) parameters.
                .SetBase58Bytes(Base58Type.PUBKEY_ADDRESS, new byte[]
                {
                    0x1E
                }) // DigiByte P2PKH addresses start with "D"
                .SetBase58Bytes(Base58Type.SCRIPT_ADDRESS, new byte[]
                {
                    0x3F
                })
                .SetBase58Bytes(Base58Type.SECRET_KEY, new byte[]
                {
                    0x80
                })
                // Set the Bech32 human-readable part for DigiByte.
                .SetBech32(Bech32Type.WITNESS_PUBKEY_ADDRESS, "dgb")
                .SetBech32(Bech32Type.WITNESS_SCRIPT_ADDRESS, "dgb")
                .BuildAndRegister();

        // Parse the bech32 address using the custom DigiByte network.
        BitcoinAddress parsedAddress = BitcoinAddress.Create(address, digiByteNetwork);

        return parsedAddress;
    }
}
