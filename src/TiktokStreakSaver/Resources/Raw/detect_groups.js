// Diagnostic script — paste in browser console on TikTok messages page
// Clicks through each chat item and detects DM vs Group

(function () {
    var items = document.querySelectorAll('[data-e2e="dm-new-conversation-item"]');
    var results = [];
    var i = 0;

    console.log('=== CHAT TYPE DETECTION ===');
    console.log('Found ' + items.length + ' chat items, scanning...\n');

    function scanNext() {
        if (i >= items.length) {
            console.log('\n=== SUMMARY ===');
            var groups = results.filter(function (r) { return r.type === 'GROUP'; });
            var dms = results.filter(function (r) { return r.type === 'DM'; });
            var unknown = results.filter(function (r) { return r.type === 'UNKNOWN'; });
            console.log('DMs: ' + dms.length + ', Groups: ' + groups.length + ', Unknown: ' + unknown.length);
            console.log('\nGroups:');
            groups.forEach(function (g) { console.log('  #' + g.index + ' — ' + g.headerText); });
            console.log('\nFull results:', results);
            return;
        }

        var item = items[i];
        item.click();

        setTimeout(function () {
            var result = { index: i + 1 };

            // Check header
            var header = document.querySelector('[class*="ChatHeader"]') ||
                document.querySelector('[class*="chatHeader"]') ||
                document.querySelector('[class*="DivChatHeader"]');

            if (!header) {
                result.type = 'UNKNOWN';
                result.reason = 'No header found';
                results.push(result);
                console.log('#' + result.index + ' — UNKNOWN (no header)');
                i++;
                scanNext();
                return;
            }

            // Header profile link (DMs have one, groups don't)
            var profileLink = header.querySelector('a[href*="/@"]');
            var headerText = header.textContent.trim().substring(0, 60);
            result.headerText = headerText;

            // Count avatars/images in the header
            var avatars = header.querySelectorAll('img');
            result.avatarCount = avatars.length;

            // Check for group-specific classes
            var groupClasses = header.querySelector('[class*="group"], [class*="Group"], [class*="member"], [class*="Member"]');
            result.hasGroupClass = !!groupClasses;

            // Check for member count text (e.g., "3 members")
            var memberMatch = headerText.match(/(\d+)\s*member/i);
            result.memberCount = memberMatch ? parseInt(memberMatch[1]) : null;

            // Check all links in header
            var allLinks = header.querySelectorAll('a');
            var linkDetails = [];
            for (var j = 0; j < allLinks.length; j++) {
                linkDetails.push({
                    href: allLinks[j].getAttribute('href') || '',
                    text: allLinks[j].textContent.trim().substring(0, 30)
                });
            }
            result.headerLinks = linkDetails;

            // Also check the chat list item itself for clues
            var itemImgs = item.querySelectorAll('img');
            result.itemAvatarCount = itemImgs.length;

            // Check for data-e2e attributes inside the item
            var e2eElements = item.querySelectorAll('[data-e2e]');
            var e2eValues = [];
            for (var k = 0; k < e2eElements.length; k++) {
                e2eValues.push(e2eElements[k].getAttribute('data-e2e'));
            }
            result.itemDataE2e = e2eValues;

            // Determine type
            if (profileLink) {
                var href = profileLink.getAttribute('href') || '';
                var match = href.match(/\/@([^\/]+)/);
                result.type = 'DM';
                result.username = match ? match[1] : '???';
                console.log('#' + result.index + ' — DM: @' + result.username);
            } else if (result.hasGroupClass || result.memberCount || avatars.length > 1) {
                result.type = 'GROUP';
                console.log('#' + result.index + ' — GROUP: "' + headerText + '" (avatars: ' + avatars.length + ')');
            } else {
                // Likely a group but no obvious markers — dump header HTML
                result.type = 'UNKNOWN';
                result.headerHTML = header.innerHTML.substring(0, 300);
                console.log('#' + result.index + ' — UNKNOWN: "' + headerText + '"');
                console.log('  Header HTML:', result.headerHTML);
            }

            results.push(result);
            i++;
            scanNext();
        }, 1500);
    }

    scanNext();
})();
