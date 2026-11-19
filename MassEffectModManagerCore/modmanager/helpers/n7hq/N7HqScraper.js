// Runs inside an n7hq.masseffect.com page. Fetches the profile pages with the user's own login and
// reads them into plain JSON for Mod Manager. Results go back through chrome.webview.postMessage.
(function () {
    'use strict';

    const pages = ['overview', 'characters', 'inventory', 'challenges'];

    // Progress only. The result comes back from the caller
    function post(msg) {
        const webview = window.chrome && window.chrome.webview;
        if (webview) {
            webview.postMessage(msg);
        }
    }

    function text(el) {
        return el ? el.textContent.replace(/\s+/g, ' ').trim() : '';
    }

    function number(s) {
        const digits = (s || '').replace(/[^0-9-]/g, '');
        return digits.length ? parseInt(digits, 10) : null;
    }

    // "/assets/images/icons/multiplayer/guns/AssaultRifle_Avenger-Lby4BNS.png" -> "guns/AssaultRifle_Avenger"
    // Asset names end in a 7 character fingerprint
    function iconKey(src) {
        const m = /\/icons\/multiplayer\/(.+?)-[A-Za-z0-9_-]{7}\.png/.exec(src || '');
        return m ? m[1] : null;
    }

    // "/assets/images/icons/emblems/SqadMstr-pJcoEP4.png" -> "SqadMstr"
    function emblemKey(src) {
        const m = /\/icons\/emblems\/(.+?)-[A-Za-z0-9_-]{7}\.png/.exec(src || '');
        return m ? m[1] : null;
    }

    function timePlayed(s) {
        const m = /(\d+)H\s*(\d+)M\s*(\d+)S/i.exec(s || '');
        return m ? (+m[1]) * 3600 + (+m[2]) * 60 + (+m[3]) : null;
    }

    function readOverview(doc) {
        return {
            timePlayedText: text(doc.querySelector('.pstimeplayed')),
            timePlayedSeconds: timePlayed(text(doc.querySelector('.pstimeplayed'))),
            gamesPlayed: number(text(doc.querySelector('.psgameplayed'))),
            credits: number(text(doc.querySelector('.pscredits'))),
            n7Rating: number(text(doc.querySelector('.n7rank'))),
            challengePoints: number(text(doc.querySelector('.pointscore'))),
            // The active challenge banner
            bannerText: text(doc.querySelector('.charactername span')),
            bannerEmblem: emblemKey((doc.querySelector('.characterinfo img[src*="/emblems/"]') || {}).getAttribute?.('src')),
            lastUpdated: text(doc.querySelector('.lastupdate span'))
        };
    }

    function readInventory(doc) {
        const items = [];
        doc.querySelectorAll('#inventory_content > div[id$="_content"]').forEach(panel => {
            const section = panel.id.replace(/_content$/, '');
            let group = null;
            Array.from(panel.children).forEach(el => {
                if (el.tagName === 'H3') {
                    group = text(el);
                    return;
                }
                if (!el.classList.contains('card')) {
                    return;
                }
                const imgs = Array.from(el.querySelectorAll('img'));
                const icon = imgs.map(i => iconKey(i.getAttribute('src'))).find(k => k && !k.startsWith('levels/'));
                const levelImg = imgs.map(i => iconKey(i.getAttribute('src'))).find(k => k && k.startsWith('levels/'));
                const levels = levelImg ? /levels\/(\d+)-(\d+)/.exec(levelImg) : null;
                items.push({
                    section: section,
                    group: group,
                    icon: icon,
                    // Owned cards use the "-on" card background
                    owned: !/-off-/.test(el.getAttribute('style') || ''),
                    level: levels ? +levels[1] : null,
                    maxLevel: levels ? +levels[2] : null,
                    lines: Array.from(el.querySelectorAll('span')).map(text)
                });
            });
        });
        return items;
    }

    function readCharacters(doc) {
        const classes = Array.from(doc.querySelectorAll('#mpClasses a[id^="mpClass_"]')).map(a => ({
            name: a.id.substring('mpClass_'.length),
            level: number(text(a.querySelector('span')))
        }));

        const deployed = {};
        doc.querySelectorAll('.mpCharacter[data-character]').forEach(el => {
            deployed[el.getAttribute('data-character')] = !el.classList.contains('characternotdeployed');
        });
        const classOf = {};
        doc.querySelectorAll('.mpCharacter[data-character]').forEach(el => {
            classOf[el.getAttribute('data-character')] = el.getAttribute('data-class-name');
        });

        // The mobile layout has the rank as text and marks the chosen evolutions
        const characters = Array.from(doc.querySelectorAll('.mp-characters-hero-info[data-character]')).map(hero => {
            const kit = hero.getAttribute('data-character');
            const about = Array.from(hero.querySelectorAll('.character-about li')).map(text);
            const field = name => {
                const li = about.find(s => s.toLowerCase().startsWith(name));
                return li ? number(li) : null;
            };
            const promotions = about.find(s => /promotions/i.test(s));
            const powers = Array.from(hero.querySelectorAll('.character-powers > dl')).map(dl => {
                const title = text(dl.querySelector('.mp-character-power-title'));
                const rank = /\((\d+)\s*\/\s*(\d+)\)/.exec(title);
                const iconSrc = (dl.querySelector('.mp-character-power-icon img') || {}).getAttribute?.('src');
                const folder = /\/powers\/([^/]+)\//.exec(iconSrc || '');
                const rows = [];
                dl.querySelectorAll('dd .character-power-row').forEach(row => {
                    const rowIcon = (row.querySelector('.character-power-row-icon') || {}).getAttribute?.('src') || '';
                    const item = (row.querySelector('.character-power-row-item') || {}).getAttribute?.('src') || '';
                    // Warp-4B-niL2Edj.png -> 4B
                    const step = /\/[^/]+-(\d[AB]?)-[A-Za-z0-9_-]{7}\.png$/.exec(rowIcon);
                    rows.push({
                        step: step ? step[1] : null,
                        title: text(row.querySelector('.character-power-row-title')),
                        active: /\/activepower/.test(item)
                    });
                });
                return {
                    name: folder ? folder[1] : null,
                    title: title.replace(/\s*\(\d+\s*\/\s*\d+\)\s*$/, ''),
                    rank: rank ? +rank[1] : null,
                    maxRank: rank ? +rank[2] : null,
                    rows: rows
                };
            });
            return {
                kit: kit,
                className: classOf[kit] || null,
                deployed: deployed[kit] !== false,
                level: field('level'),
                xp: field('xp'),
                promotions: promotions ? number(promotions) : null,
                powers: powers
            };
        });
        return { classes: classes, characters: characters };
    }

    // "Current progress: 24/25" -> [24, 25]
    function fraction(spans, label) {
        const s = spans.find(x => x.toLowerCase().startsWith(label));
        const m = s ? /([\d,]+)\s*\/\s*([\d,]+)/.exec(s) : null;
        return m ? [number(m[1]), number(m[2])] : null;
    }

    function labelled(spans, label) {
        const s = spans.find(x => x.toLowerCase().startsWith(label));
        return s ? number(s) : null;
    }

    // Each group's list id is g<accomplishment index>
    function readChallenges(doc) {
        return Array.from(doc.querySelectorAll('.mainchallengeitem')).map(item => {
            const list = item.querySelector('.silverblockchallenge[id^="g"]');
            const gold = item.querySelector('.goldchallengeitem');
            if (!list || !gold) {
                return null;
            }
            const h4 = gold.querySelector('h4');
            const repeat = h4 ? h4.querySelector('span') : null;
            const spans = Array.from(gold.querySelectorAll('.goldchallengedetails span')).map(text);
            const banner = gold.querySelector('.golditembanner');
            return {
                id: number(list.id),
                title: h4 ? text(h4).replace(text(repeat), '').trim() : null,
                // "x8" after the title is how many times the group was completed
                timesCompleted: repeat ? number(text(repeat)) : 0,
                complete: gold.classList.contains('challengecomplete'),
                progress: fraction(spans, 'current progress'),
                points: labelled(spans, 'challenge points'),
                emblem: emblemKey(banner && banner.getAttribute('style')),
                emblemText: text(gold.querySelector('.golditememblemtext')),
                children: Array.from(list.querySelectorAll('.silverchallenge')).map(c => {
                    const info = Array.from(c.querySelectorAll('.silverinfo span')).map(text);
                    return {
                        title: text(c.querySelector('h5')),
                        description: info.length ? info[0] : null,
                        progress: fraction(info, 'current progress'),
                        points: labelled(info, 'challenge points'),
                        timesCompleted: labelled(info, 'times completed'),
                        complete: c.classList.contains('silvercomplete')
                    };
                })
            };
        }).filter(g => g);
    }

    // Weekend operations aren't in the groups above
    function readPastChallenges(doc) {
        return Array.from(doc.querySelectorAll('.pastchallenge')).map(el => ({
            title: text(el.querySelector('p')),
            date: text(el.querySelector('span')),
            complete: !el.classList.contains('challfail')
        }));
    }

    async function fetchPage(url) {
        const response = await fetch(url, { credentials: 'include' });
        if (!response.ok) {
            throw new Error(`N7HQ returned ${response.status} for ${url}`);
        }
        return await response.text();
    }

    // getPage is only passed in by tests
    window.__m3N7HqScrape = async function (getPage) {
        getPage = getPage || fetchPage;
        const query = new URLSearchParams(location.search);
        const name = query.get('name');
        const platform = query.get('platform');
        if (!name || !platform) {
            throw new Error('Open your N7HQ profile page first.');
        }

        const parser = new DOMParser();
        const html = {};
        const docs = {};
        for (let i = 0; i < pages.length; i++) {
            const page = pages[i];
            post({ type: 'progress', text: `Reading ${page} (${i + 1}/${pages.length})` });
            const url = `/en/home/${page}?name=${encodeURIComponent(name)}&platform=${encodeURIComponent(platform)}`;
            html[page] = await getPage(url);
            docs[page] = parser.parseFromString(html[page], 'text/html');
        }

        const characters = readCharacters(docs.characters);
        return {
            format: 'N7HQ-profile-scrape',
            version: 1,
            scrapedUtc: new Date().toISOString(),
            name: name,
            platform: platform,
            overview: readOverview(docs.overview),
            classes: characters.classes,
            characters: characters.characters,
            inventory: readInventory(docs.inventory),
            challenges: readChallenges(docs.challenges),
            pastChallenges: readPastChallenges(docs.challenges),
            pages: html
        };
    };
})();
