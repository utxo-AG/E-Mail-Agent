// SPDX-License-Identifier: MIT
pragma solidity ^0.8.0;

import "@openzeppelin/contracts/token/ERC721/extensions/ERC721URIStorage.sol";
import "@openzeppelin/contracts/token/ERC721/extensions/ERC721Enumerable.sol";
import "@openzeppelin/contracts/access/Ownable.sol";
import "@openzeppelin/contracts/utils/Counters.sol";
import "@openzeppelin/contracts/utils/Strings.sol";

/**
 * @title SaschaTestNFT
 * @dev ERC-721 NFT Contract für SaschaTest NFT Collection
 * Erstellt für RST Datentechnik GmbH
 */
contract SaschaTestNFT is ERC721URIStorage, ERC721Enumerable, Ownable {
    using Counters for Counters.Counter;
    using Strings for uint256;
    
    Counters.Counter private _tokenIds;

    // NFT Eigenschaften
    struct NFTAttributes {
        string color;
        uint256 weight;
        uint256 rarity;
    }
    
    mapping(uint256 => NFTAttributes) public tokenAttributes;
    
    // Collection Settings
    uint256 public constant MAX_SUPPLY = 1000;
    string private _baseTokenURI = "ipfs://";
    
    constructor() ERC721("SaschaTest", "STEST") {
        // Mint initial NFT with specified attributes
        mintNFT(
            msg.sender,
            "SaschaTest", 
            "ipfs://sj83uhdjajjhsuiu3372b2bdhsjhwegvsgajhsdbhasd",
            "black",
            80,
            5
        );
    }

    /**
     * @dev Mint NFT with custom attributes
     * @param to Address to mint to
     * @param name Name of the NFT
     * @param imageURI IPFS URI of the image
     * @param color Color attribute
     * @param weight Weight attribute
     * @param rarity Rarity attribute (1-10 scale)
     */
    function mintNFT(
        address to,
        string memory name,
        string memory imageURI,
        string memory color,
        uint256 weight,
        uint256 rarity
    ) public onlyOwner returns (uint256) {
        require(_tokenIds.current() < MAX_SUPPLY, "Max supply reached");
        require(rarity >= 1 && rarity <= 10, "Rarity must be between 1-10");
        
        _tokenIds.increment();
        uint256 newTokenId = _tokenIds.current();
        
        _safeMint(to, newTokenId);
        
        // Store attributes
        tokenAttributes[newTokenId] = NFTAttributes({
            color: color,
            weight: weight,
            rarity: rarity
        });
        
        // Set token URI to the provided image URI
        _setTokenURI(newTokenId, imageURI);
        
        return newTokenId;
    }

    /**
     * @dev Generate metadata JSON for a token
     * @param tokenId Token ID to generate metadata for
     */
    function getTokenMetadata(uint256 tokenId) public view returns (string memory) {
        require(_exists(tokenId), "Token does not exist");
        
        NFTAttributes memory attrs = tokenAttributes[tokenId];
        
        return string(abi.encodePacked(
            '{"name": "SaschaTest #', tokenId.toString(), '",',
            '"description": "Ein NFT erstellt für Sascha Tobler von RST Datentechnik GmbH",',
            '"image": "', tokenURI(tokenId), '",',
            '"attributes": [',
                '{"trait_type": "Color", "value": "', attrs.color, '"},',
                '{"trait_type": "Weight", "value": ', attrs.weight.toString(), ', "display_type": "number"},',
                '{"trait_type": "Rarity", "value": ', attrs.rarity.toString(), ', "display_type": "number", "max_value": 10}',
            ']}'
        ));
    }

    /**
     * @dev Get all tokens owned by an address
     * @param owner Address to get tokens for
     */
    function tokensOfOwner(address owner) public view returns (uint256[] memory) {
        uint256 tokenCount = balanceOf(owner);
        uint256[] memory tokens = new uint256[](tokenCount);
        
        uint256 index = 0;
        for (uint256 i = 1; i <= _tokenIds.current(); i++) {
            if (ownerOf(i) == owner) {
                tokens[index] = i;
                index++;
            }
        }
        
        return tokens;
    }

    /**
     * @dev Get current supply
     */
    function totalMinted() public view returns (uint256) {
        return _tokenIds.current();
    }

    // Required overrides for multiple inheritance
    function _beforeTokenTransfer(
        address from,
        address to,
        uint256 tokenId,
        uint256 batchSize
    ) internal override(ERC721, ERC721Enumerable) {
        super._beforeTokenTransfer(from, to, tokenId, batchSize);
    }

    function _burn(uint256 tokenId) internal override(ERC721, ERC721URIStorage) {
        super._burn(tokenId);
    }

    function tokenURI(uint256 tokenId) 
        public 
        view 
        override(ERC721, ERC721URIStorage) 
        returns (string memory) 
    {
        return super.tokenURI(tokenId);
    }

    function supportsInterface(bytes4 interfaceId)
        public
        view
        override(ERC721, ERC721Enumerable)
        returns (bool)
    {
        return super.supportsInterface(interfaceId);
    }
}