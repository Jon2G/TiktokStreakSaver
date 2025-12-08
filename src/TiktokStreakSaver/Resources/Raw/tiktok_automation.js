// TikTok Message Automation Script
// This script is injected into the TikTok WebView to automate messaging

(function() {
    'use strict';

    // Configuration
    const CONFIG = {
        searchDelay: 2000,
        clickDelay: 1500,
        typeDelay: 500,
        sendDelay: 1000,
        maxRetries: 3
    };

    // Selectors for TikTok elements (may need updates as TikTok changes their UI)
    const SELECTORS = {
        // Search and navigation
        searchInput: [
            'input[placeholder*="Search"]',
            'input[placeholder*="search"]',
            '[data-e2e="search-input"]',
            '[class*="SearchInput"]',
            '[class*="search-input"]'
        ],
        
        // Conversation list
        conversationItem: [
            '[class*="DivConversationListItem"]',
            '[class*="ConversationItem"]',
            '[class*="conversation-item"]',
            '[class*="chat-item"]',
            '[data-e2e="conversation-item"]'
        ],
        
        // User/contact in search results
        userItem: [
            '[class*="UserItem"]',
            '[class*="user-item"]',
            '[class*="SearchResultItem"]',
            '[data-e2e="search-user-item"]'
        ],
        
        // Message input
        messageInput: [
            '[data-e2e="message-input"]',
            'textarea[placeholder*="Send"]',
            'textarea[placeholder*="Message"]',
            'div[contenteditable="true"][class*="message"]',
            'div[contenteditable="true"][data-placeholder]',
            '[class*="MessageInput"] textarea',
            '[class*="message-input"] textarea'
        ],
        
        // Send button
        sendButton: [
            '[data-e2e="send-button"]',
            'button[type="submit"]',
            '[class*="SendButton"]',
            '[class*="send-button"]',
            'button[class*="send"]'
        ]
    };

    // Utility functions
    function findElement(selectorList) {
        for (const selector of selectorList) {
            try {
                const element = document.querySelector(selector);
                if (element) return element;
            } catch (e) {
                // Invalid selector, continue to next
            }
        }
        return null;
    }

    function findElements(selectorList) {
        const results = [];
        for (const selector of selectorList) {
            try {
                const elements = document.querySelectorAll(selector);
                elements.forEach(el => results.push(el));
            } catch (e) {
                // Invalid selector, continue
            }
        }
        return results;
    }

    function delay(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    function simulateInput(element, text) {
        element.focus();
        
        // Clear existing content
        if (element.tagName === 'TEXTAREA' || element.tagName === 'INPUT') {
            element.value = '';
            element.value = text;
        } else {
            element.textContent = '';
            element.textContent = text;
        }
        
        // Dispatch events to trigger React/Vue handlers
        element.dispatchEvent(new Event('focus', { bubbles: true }));
        element.dispatchEvent(new Event('input', { bubbles: true }));
        element.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function simulateEnter(element) {
        const enterEvent = new KeyboardEvent('keydown', {
            key: 'Enter',
            code: 'Enter',
            keyCode: 13,
            which: 13,
            bubbles: true,
            cancelable: true
        });
        element.dispatchEvent(enterEvent);
        
        const enterUpEvent = new KeyboardEvent('keyup', {
            key: 'Enter',
            code: 'Enter',
            keyCode: 13,
            which: 13,
            bubbles: true,
            cancelable: true
        });
        element.dispatchEvent(enterUpEvent);
    }

    // Main automation class
    class TikTokMessenger {
        constructor() {
            this.callbacks = {};
        }

        onResult(callback) {
            this.callbacks.result = callback;
        }

        reportResult(username, success, error) {
            if (typeof StreakApp !== 'undefined' && StreakApp.onMessageSent) {
                StreakApp.onMessageSent(username, success, error || '');
            }
            if (this.callbacks.result) {
                this.callbacks.result(username, success, error);
            }
        }

        async findConversation(username) {
            // First, try to find in existing conversation list
            const conversations = findElements(SELECTORS.conversationItem);
            const usernameLC = username.toLowerCase();
            
            for (const conv of conversations) {
                const text = conv.textContent?.toLowerCase() || '';
                if (text.includes(usernameLC)) {
                    return conv;
                }
            }
            
            // If not found, try using search
            const searchInput = findElement(SELECTORS.searchInput);
            if (searchInput) {
                simulateInput(searchInput, username);
                await delay(CONFIG.searchDelay);
                
                // Look in search results
                const userItems = findElements(SELECTORS.userItem);
                for (const item of userItems) {
                    const text = item.textContent?.toLowerCase() || '';
                    if (text.includes(usernameLC)) {
                        return item;
                    }
                }
                
                // Also check conversations again after search
                const updatedConversations = findElements(SELECTORS.conversationItem);
                for (const conv of updatedConversations) {
                    const text = conv.textContent?.toLowerCase() || '';
                    if (text.includes(usernameLC)) {
                        return conv;
                    }
                }
            }
            
            return null;
        }

        async sendMessage(username, message) {
            try {
                console.log(`[StreakSaver] Looking for conversation with: ${username}`);
                
                // Find and click on the conversation
                const conversation = await this.findConversation(username);
                if (!conversation) {
                    this.reportResult(username, false, 'User not found in conversations or search');
                    return false;
                }

                console.log(`[StreakSaver] Found conversation, clicking...`);
                conversation.click();
                await delay(CONFIG.clickDelay);

                // Find message input
                const messageInput = findElement(SELECTORS.messageInput);
                if (!messageInput) {
                    this.reportResult(username, false, 'Message input not found');
                    return false;
                }

                console.log(`[StreakSaver] Found message input, typing message...`);
                simulateInput(messageInput, message);
                await delay(CONFIG.typeDelay);

                // Try to send via button first
                const sendButton = findElement(SELECTORS.sendButton);
                if (sendButton && !sendButton.disabled) {
                    console.log(`[StreakSaver] Clicking send button...`);
                    sendButton.click();
                } else {
                    // Fallback to Enter key
                    console.log(`[StreakSaver] Pressing Enter to send...`);
                    simulateEnter(messageInput);
                }

                await delay(CONFIG.sendDelay);
                
                console.log(`[StreakSaver] Message sent successfully to ${username}`);
                this.reportResult(username, true, '');
                return true;

            } catch (error) {
                console.error(`[StreakSaver] Error sending message to ${username}:`, error);
                this.reportResult(username, false, error.message || 'Unknown error');
                return false;
            }
        }

        async sendToMultiple(usernames, message, delayBetween = 3000) {
            const results = [];
            for (const username of usernames) {
                const success = await this.sendMessage(username, message);
                results.push({ username, success });
                
                if (usernames.indexOf(username) < usernames.length - 1) {
                    await delay(delayBetween);
                }
            }
            return results;
        }

        checkLoginStatus() {
            const url = window.location.href.toLowerCase();
            
            // Check for login page
            if (url.includes('/login')) {
                return { loggedIn: false, reason: 'On login page' };
            }
            
            // Check for messages page or home page (indicates logged in)
            if (url.includes('/messages') || url.includes('/foryou') || url.includes('/@')) {
                return { loggedIn: true };
            }
            
            // Check for user-specific elements
            const userElements = document.querySelectorAll('[data-e2e="profile-icon"], [class*="Avatar"]');
            if (userElements.length > 0) {
                return { loggedIn: true };
            }
            
            return { loggedIn: false, reason: 'Unknown page state' };
        }
    }

    // Expose to global scope
    window.TikTokMessenger = TikTokMessenger;
    
    // Create default instance
    window.streakMessenger = new TikTokMessenger();

    console.log('[StreakSaver] TikTok automation script loaded');
})();





