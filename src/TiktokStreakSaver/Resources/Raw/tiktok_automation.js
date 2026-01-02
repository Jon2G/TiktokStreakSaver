// TikTok Message Automation Script
// This script is injected into the TikTok WebView to automate messaging

(function () {
    var userName = '[UserName]';
    var message = '[Message]';
    var found = false;
    var chatIndex = 0;
    var chatItems = [];

    var log = function (msg) {
        if (typeof StreakApp == 'undefined') {
            console.log(msg);
            return;
        }
        StreakApp.log(msg);
    };

    var findChatItems = function () {
        return document.querySelectorAll("[data-e2e*='chat-list-item']");
    };

    var findCurrentChatUsername = function () {
        // Find the username from the chat header (the opened conversation)
        var chatHeader = document.querySelector('[class*="ChatHeader"]') ||
                         document.querySelector('[class*="chatHeader"]') ||
                         document.querySelector('[class*="DivChatHeader"]');
        
        if (chatHeader) {
            var headerLink = chatHeader.querySelector('a[href*="/@"]');
            if (headerLink) {
                var href = headerLink.getAttribute('href') || '';
                var match = href.match(/\/@([^\/]+)/);
                return match ? match[1] : '';
            }
        }
        
        // Fallback: look for links with no data-e2e parent (usually header area)
        var links = document.querySelectorAll('[class*="StyledLink"]');
        for (var i = 0; i < links.length; i++) {
            var link = links[i];
            var parent = link.closest('[data-e2e]');
            var parentAttr = parent ? parent.getAttribute('data-e2e') : '';
            
            // Skip inbox items, only look at header/none area
            if (!parentAttr || parentAttr === 'chat-header') {
                var href = link.getAttribute('href') || '';
                var match = href.match(/\/@([^\/]+)/);
                if (match && match[1]) {
                    return match[1];
                }
            }
        }
        
        return '';
    };

    var findMessageInput = function () {
        // Find the contenteditable editor container for Draft.js
        var editor = document.querySelector('[class*="DraftEditor-editorContainer"] [contenteditable="true"]') ||
            document.querySelector('[class*="DraftEditor-root"] [contenteditable="true"]') ||
            document.querySelector('div[contenteditable="true"][role="textbox"]') ||
            document.querySelector('div[contenteditable="true"]');

        return editor;
    };

    var findMessageButton = function () {
        return document.querySelector("[data-e2e*='message-button']") ||
            document.querySelector("[data-e2e*='message-send']");
    };

    var isTargetUser = function (currentUsername) {
        return currentUsername && currentUsername.toLowerCase().includes(userName.toLowerCase());
    };

    var findDraftEditor = function (messageInput) {
        // Find React fiber and Draft.js editor instance
        var key = Object.keys(messageInput).find(function(k) {
            return k.startsWith('__reactFiber$') || k.startsWith('__reactInternalInstance$');
        });
        
        if (!key) {
            log('React fiber not found');
            return null;
        }
        
        var fiber = messageInput[key];
        var current = fiber;
        
        while (current) {
            if (current.stateNode && current.stateNode.editor) {
                return current.stateNode;
            }
            current = current.return;
        }
        
        log('Draft editor instance not found');
        return null;
    };

    var typeMessage = function (messageInput, callback) {
        log('Starting typeMessage...');
        
        // Find Draft.js editor instance
        var draftEditor = findDraftEditor(messageInput);
        
        if (draftEditor) {
            log('Found Draft.js editor, using _onPaste method');
            
            // Focus the editor using Draft.js focus method
            draftEditor.focus();
            
            setTimeout(function () {
                // Create a paste event with DataTransfer containing our message
                var dataTransfer = new DataTransfer();
                dataTransfer.setData('text/plain', message);
                
                var pasteEvent = new ClipboardEvent('paste', {
                    bubbles: true,
                    cancelable: true,
                    clipboardData: dataTransfer
                });
                
                // Call Draft.js internal _onPaste handler directly
                try {
                    draftEditor._onPaste(pasteEvent);
                    log('_onPaste called successfully');
                } catch (e) {
                    log('_onPaste error: ' + e.message);
                }
                
                setTimeout(function () {
                    log('Content after _onPaste: "' + messageInput.textContent + '"');
                    callback();
                }, 300);
            }, 200);
        } else {
            // Fallback: try execCommand if Draft.js not found
            log('Draft.js not found, falling back to execCommand');
            
            messageInput.click();
            messageInput.focus();
            
            setTimeout(function () {
                var selection = window.getSelection();
                var range = document.createRange();
                range.selectNodeContents(messageInput);
                range.collapse(false);
                selection.removeAllRanges();
                selection.addRange(range);
                
                document.execCommand('insertText', false, message);
                log('Content after execCommand: "' + messageInput.textContent + '"');
                
                setTimeout(callback, 300);
            }, 200);
        }
    };

    var sendMessage = function (messageInput) {
        debugger;
        // Try to find and click send button first
        var sendBtn = document.querySelector('[data-e2e*="send"]') ||
                      document.querySelector('[data-e2e*="Send"]') ||
                      document.querySelector('button[type="submit"]');
        
        if (sendBtn) {
            log('Found send button, clicking...');
            sendBtn.dispatchEvent(new Event('click', { bubbles: true }));
            return;
        }
        
        // Fallback: press Enter key
        log('No send button found, pressing Enter...');
        messageInput.dispatchEvent(new KeyboardEvent('keydown', {
            key: 'Enter',
            code: 'Enter',
            keyCode: 13,
            which: 13,
            bubbles: true,
            cancelable: true
        }));

        messageInput.dispatchEvent(new KeyboardEvent('keyup', {
            key: 'Enter',
            code: 'Enter',
            keyCode: 13,
            which: 13,
            bubbles: true
        }));
    };

    var reportSuccess = function () {
        log('Message sent to ' + userName);
        if (typeof StreakApp !== 'undefined') {
        StreakApp.onMessageSent(userName, true, '');
        }
    };

    var reportError = function (errorMessage) {
        log(errorMessage);
         if (typeof StreakApp !== 'undefined') {
        StreakApp.onMessageSent(userName, false, errorMessage);
        }
    };

    var sendMessageDirect = function () {
        var messageInput = findMessageInput();

        if (messageInput) {
            typeMessage(messageInput, function () {
                sendMessage(messageInput);
                setTimeout(reportSuccess, 1000);
            });
        } else {
            reportError('Message input not found');
        }
    };

    var sendMessageViaButton = function () {
        var messageInput = findMessageInput();
        
        if (messageInput) {
            log('Found message input, typing...');
            typeMessage(messageInput, function () {
                sendMessage(messageInput);
                setTimeout(reportSuccess, 1000);
            });
        } else {
            reportError('Message input not found');
        }
    };

    var searchForUserInCurrentChat = function () {
        // Get the username from the chat header (current open conversation)
        var currentUsername = findCurrentChatUsername();
        log('Current chat username: ' + currentUsername);

        if (currentUsername && isTargetUser(currentUsername)) {
            found = true;
            log('Found target user: ' + currentUsername);
            sendMessageViaButton();
            return true;
        }
        
        log('Not the target user, moving to next chat...');
        return false;
    };

    var checkNextChat = function () {
        if (found || chatIndex >= chatItems.length) {
            if (!found) {
                log('User not found after checking all chats');
                reportError('User not found in chat list');
            }
            return;
        }

        var chatItem = chatItems[chatIndex];
        log('Clicking chat item ' + (chatIndex + 1) + '/' + chatItems.length);
        chatItem.click();

        setTimeout(function () {
            var userFound = searchForUserInCurrentChat();

            if (!userFound) {
                chatIndex++;
                checkNextChat();
            }
        }, 1500);
    };

    var init = function () {
        try {
            if (userName.startsWith('@')) {
                userName = userName.substring(1);
            }
            log('Looking for user: ' + userName);

            chatItems = findChatItems();
            log('Found ' + chatItems.length + ' chat items');

            if (chatItems.length === 0) {
                reportError('No chat items found');
                return;
            }

            checkNextChat();

        } catch (e) {
            log('Error: ' + e.message);
            reportError('Error: ' + e.message);
        }
    };

    // Start the automation
    init();
})();
