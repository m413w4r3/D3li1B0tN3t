using System.Security.Cryptography;
using D3B.Protocol.Mapping;

namespace D3B.Protocol.Tests.Mapping;

/// <summary>
/// Fails when what a mapping depends on changes while <see cref="MappingFile.FormatVersion"/> stays: a host would then
/// read the mappings of its own format with another layout, or for another readable protocol (D45).
/// </summary>
/// <remarks>
/// The contract is the SHA-256 of the digests of <c>mapping.proto</c> and of the readable protocol
/// (<see cref="ProtocolDigest"/>): renaming a field, a value or a oneof, or editing a comment, changes neither.
/// </remarks>
public sealed class MappingFormatTests
{
    // The contract of each format, oldest first. Once a mapping of a format is published, its line is never edited: a
    // changed contract is a new format, and a new line.
    private static readonly (int Format, string Contract)[] _formats =
    [
        (1, "7ff4eda625c85f7c873a13e1a399934d4e83273f4faa67f902b4cde344fa32cd"),
        (2, "3fbef5dd21d2e3ec2affad1b747c1577fee40660f35e65be4c618eb1ae7a25f2"),
        (3, "c17f8d4413836a8194ab401daa0686834bebd40c428240b98938bd5a3ffbf828"),
        (4, "9358492b69c96c2a7ba929fa4d1ec67bdf15e67fde17fb68499e7faf44ffbd7f"),
        (5, "a0d4c8975ae69097049ad5ea96c058651d8d76ed4ea05f3de0601a8c8528b1a5"),
        (6, "f0dbae13c108a52d09f6908f89e32651b183ec77e8f149ff04ead29bdd4e6973"),
        (7, "bc0a45ae99aca344ae35b6327b3d382f7b128b38b9ee80b294cd614087b85084"),
        (8, "8015a6cbecb97257dd0bc08884cf24d1857a528b6b5d895cf1da7941c6d7c59e"),
        (9, "150c0cbc893ef0e6159ab196732e4758c7ba23ffaabc73f3c7e692c0267437f6"),
        (10, "5ec7cc73c7db0e108a40ccb36934de113e6fb5654625b6827cec17bc5bf35192"),
        (11, "b584d17a16eab11788d730dd9a260a347309f2a71e16f5fa95bdb20d3c12bfcb"),
        (12, "b319c4e5ac456a7dbac53fbf491bd9723eae3d8eeb5e0fb268b5cf249d396fde"),
        (13, "9a880bbac593d718b157be0e373fd815530d7ac2425091e4e30271ef394696a5"),
        (14, "bbe2ecf5618b22f7bd99b4800d4379aa53f8949f2f98056de25e66b86e302f05"),
        (15, "873f45d9dfbbc953bb7f534eb22d2baed28ea8411ff6b67b0519178ca5ee41f5"),
    ];

    private static string Contract()
    {
        return Convert.ToHexStringLower(SHA256.HashData([.. ProtocolDigest.Of([MappingReflection.Descriptor]), .. ReadableProtocol.Digest]));
    }

    [Fact]
    public void TheLastRecordedFormatIsTheCurrentOne()
    {
        var format = _formats[^1].Format;

        Assert.True(
            format == MappingFile.FormatVersion,
            $"MappingFile.FormatVersion is {MappingFile.FormatVersion}, where the last recorded format is {format}: record the contract of format {MappingFile.FormatVersion}.");

        for (var i = 1; i < _formats.Length; i++)
            Assert.True(_formats[i].Format > _formats[i - 1].Format, "The recorded formats must be in ascending order.");
    }

    [Fact]
    public void TheContractChangesOnlyWithTheFormatVersion()
    {
        var (format, recorded) = _formats[^1];
        var current = Contract();

        Assert.True(
            current == recorded,
            $"The mapping's contract is now {current}, where format {format} recorded {recorded}: raise MappingFile.FormatVersion, then record the new contract under it.");
    }
}
