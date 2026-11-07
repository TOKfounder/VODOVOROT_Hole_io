mergeInto(LibraryManager.library, {
    VodovorotAchievementOpen: function(jsonPointer, portraitPointer) {
        var data;
        try { data = JSON.parse(UTF8ToString(jsonPointer)); } catch (_) { return; }
        var portrait = UTF8ToString(portraitPointer);
        var previous = document.getElementById('vodovorot-achievement');
        if (previous && previous.vodoClose) previous.vodoClose();
        var ru = data.ru === true;
        var text = function(russian, english) { return ru ? russian : english; };
        var color = function(value, fallback) { return /^#[0-9a-f]{6}$/i.test(value || '') ? value : fallback; };
        var ink = color(data.webInk, '#111b22'), paper = color(data.webPaper, '#eefaff');
        var blue = color(data.webBlue, '#1f9de7'), green = color(data.webGreen, '#59d625'), amber = color(data.webYellow, '#ffc42f');
        // Only the public game URL is shared. Names and neighbouring players are never exported.
        var url = /^https:\/\/yandex\.ru\/games\/app\/[0-9]+$/.test(data.url) ? data.url : 'https://yandex.ru/games/app/473763';
        var overlay = document.createElement('div');
        overlay.id = 'vodovorot-achievement';
        overlay.setAttribute('role', 'dialog'); overlay.setAttribute('aria-modal', 'true');
        overlay.setAttribute('aria-label', text('Карточка достижения', 'Achievement card'));
        overlay.tabIndex = -1;
        // Keep the game's global focus/context-menu handlers from interfering with the share sheet or manual copy.
        overlay.addEventListener('pointerdown', function(event) { event.stopPropagation(); });
        overlay.addEventListener('contextmenu', function(event) { event.stopPropagation(); });
        overlay.style.cssText = 'position:fixed;inset:0;z-index:2147483646;background:rgba(5,24,36,.94);color:'+paper+';display:flex;flex-direction:column;align-items:center;justify-content:flex-start;box-sizing:border-box;padding:max(12px,env(safe-area-inset-top)) 12px max(12px,env(safe-area-inset-bottom));gap:10px;overflow:auto;font:16px "Comic Sans MS",cursive;user-select:auto;overscroll-behavior:contain;';
        var header = document.createElement('div');
        header.style.cssText = 'display:flex;align-items:center;justify-content:space-between;width:min(100%,560px);flex-shrink:0;';
        var heading = document.createElement('strong'); heading.textContent = text('Моё достижение', 'My achievement');
        heading.style.cssText = 'font-size:20px;';
        // The large heading and all other card lettering are rendered with the game's actual font in Unity.
        if (!data.fullCardImage) header.appendChild(heading); else header.style.justifyContent='flex-end';
        var buttonStyle = 'box-sizing:border-box;border:0;border-radius:10px;min-height:52px;width:260px;max-width:100%;padding:0;font:bold 16px "Comic Sans MS",cursive;cursor:pointer;background:transparent;color:#fff;touch-action:manipulation;transition:filter .08s,transform .08s;';
        var makeButton = function(label, artwork, background, fn) {
            var button = document.createElement('button'); button.type = 'button'; button.setAttribute('aria-label',label);
            button.style.cssText = buttonStyle;
            if (artwork && /^[a-z0-9+/=]+$/i.test(artwork)) {
                var art = document.createElement('img'); art.src='data:image/png;base64,'+artwork;
                art.alt=label; art.draggable=false; art.style.cssText='display:block;width:100%;height:52px;pointer-events:none;';
                button.appendChild(art);
            } else { button.textContent=label; button.style.background=background; button.style.border='3px solid #000'; }
            button.addEventListener('pointerdown',function(){if(!button.disabled){button.style.filter='brightness(.8)';button.style.transform='translateY(2px)';}});
            var release=function(){button.style.filter=button.disabled?'brightness(.6)':'';button.style.transform='';};
            button.addEventListener('pointerup',release); button.addEventListener('pointercancel',release); button.addEventListener('pointerleave',release);
            button.addEventListener('click', fn); return button;
        };
        var objectURL = null;
        var closed = false;
        var activeBefore = document.activeElement;
        var keyHandler, focusHandler;
        var close = function() {
            if (closed) return;
            closed = true;
            document.removeEventListener('keydown', keyHandler);
            document.removeEventListener('focusin', focusHandler, true);
            if (overlay.parentNode) overlay.parentNode.removeChild(overlay);
            if (objectURL) { var revokedURL = objectURL; setTimeout(function() { URL.revokeObjectURL(revokedURL); }, 1500); }
            if (activeBefore && activeBefore.focus) activeBefore.focus({preventScroll:true});
        };
        overlay.vodoClose = close;
        var closeButton = makeButton(text('Закрыть', 'Close'), data.webCloseButton, amber, close);
        closeButton.style.width='150px'; header.appendChild(closeButton);
        overlay.appendChild(header);
        var canvas = document.createElement('canvas'); canvas.width = 1080; canvas.height = 1350;
        canvas.setAttribute('aria-label', text('Карточка с результатом игры', 'Game result card'));
        canvas.style.cssText = 'display:block;max-width:min(100%,560px);max-height:calc(100dvh - 260px);width:auto;height:auto;object-fit:contain;border-radius:12px;flex-shrink:1;min-height:180px;';
        overlay.appendChild(canvas);
        var actions = document.createElement('div'); actions.style.cssText = 'display:flex;flex-wrap:wrap;justify-content:center;gap:8px;width:min(100%,620px);flex-shrink:0;';
        overlay.appendChild(actions);
        var status = document.createElement('div'); status.setAttribute('role','status');
        status.style.cssText = 'max-width:560px;text-align:center;font-size:14px;line-height:1.4;min-height:20px;flex-shrink:0;';
        overlay.appendChild(status);
        var shareFile = null;
        var manualInput = null;
        var shareText = text('Мой Водоворот: ', 'My Vodovorot: ') + data.percent + ' · ' + data.map;
        if (data.rank > 0) shareText += text(' · место ', ' · rank ') + data.rank;
        var shareButton = makeButton(text('Отправить другу', 'Share'), data.webShareButton, amber, function() {
            if (!navigator.share) {
                status.textContent = text('Сохраните карточку и скопируйте ссылку — их можно отправить в любом мессенджере.', 'Save the card and copy the link to send them in any messenger.'); return;
            }
            var payload = {title:text('Водоворот — моё достижение', 'Vodovorot — my achievement'), text:shareText, url:url};
            try {
                if (shareFile && navigator.canShare && navigator.canShare({files:[shareFile]})) {
                    payload = {title:payload.title, text:shareText + '\n' + url, files:[shareFile]};
                }
                // This is a real DOM click, retaining the transient user activation required by Web Share.
                navigator.share(payload).then(function() {
                    if (closed) return;
                    status.textContent = text('Передано в меню отправки.', 'Passed to the share menu.');
                }).catch(function(error) {
                    if (closed) return;
                    status.textContent = error && error.name === 'AbortError'
                        ? text('Отправка отменена.', 'Sharing cancelled.')
                        : text('Отправка здесь недоступна. Сохраните карточку и скопируйте ссылку.', 'Sharing is unavailable here. Save the card and copy the link.');
                });
            } catch (_) { status.textContent = text('Сохраните карточку и скопируйте ссылку.', 'Save the card and copy the link.'); }
        });
        shareButton.disabled = true;
        var saveButton = makeButton(text('Сохранить PNG', 'Save PNG'), data.webSaveButton, green, function() {
            if (!objectURL) return;
            var link = document.createElement('a'); link.href = objectURL; link.download = 'vodovorot-achievement.png';
            document.body.appendChild(link); link.click(); link.remove();
            status.textContent = text('Картинка передана браузеру для сохранения.', 'Image passed to your browser for saving.');
        }); saveButton.disabled = true;
        var copyButton = makeButton(text('Скопировать ссылку', 'Copy link'), data.webCopyButton, blue, function() {
            var manual = function() {
                if (closed || !overlay.parentNode) return;
                var input = manualInput || document.createElement('input'); manualInput = input;
                input.value = url; input.readOnly = true;
                input.setAttribute('aria-label', text('Ссылка на игру', 'Game link'));
                input.style.cssText = 'box-sizing:border-box;width:min(90%,560px);min-height:52px;border:3px solid #000;border-radius:10px;padding:8px;font:16px sans-serif;background:'+paper+';color:'+ink+';';
                if (!input.parentNode) overlay.appendChild(input);
                input.focus(); input.select();
                var copied = false;
                try { copied = document.execCommand('copy'); } catch (_) { }
                if (copied) { input.remove(); copyButton.focus({preventScroll:true}); status.textContent = text('Ссылка скопирована.', 'Link copied.'); }
                else status.textContent = text('Скопируйте выделенную ссылку вручную.', 'Copy the selected link manually.');
            };
            if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(url).then(function() {
                if (closed) return;
                status.textContent = text('Ссылка скопирована.', 'Link copied.');
            }).catch(manual);
            else manual();
        });
        actions.appendChild(shareButton); actions.appendChild(saveButton); actions.appendChild(copyButton);
        keyHandler = function(event) {
            if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); close(); }
            if (event.key === 'Tab') {
                var focusable = Array.prototype.slice.call(overlay.querySelectorAll('button:not(:disabled),input'));
                var first = focusable[0], last = focusable[focusable.length-1];
                if (event.shiftKey && document.activeElement === first) { last.focus(); event.preventDefault(); }
                else if (!event.shiftKey && document.activeElement === last) { first.focus(); event.preventDefault(); }
            }
        };
        focusHandler = function(event) {
            // The WebGL template can refocus its canvas on resize/fullscreen.
            // Keep keyboard input in the open modal, including after legacy copy.
            if (!closed && overlay.parentNode && !overlay.contains(event.target)) closeButton.focus({preventScroll:true});
        };
        document.addEventListener('keydown', keyHandler);
        document.addEventListener('focusin', focusHandler, true);
        document.body.appendChild(overlay); closeButton.focus();
        var context = canvas.getContext('2d');
        var roundRect = function(x,y,w,h,r,color) {
            context.fillStyle=color; context.beginPath(); context.moveTo(x+r,y); context.arcTo(x+w,y,x+w,y+h,r);
            context.arcTo(x+w,y+h,x,y+h,r); context.arcTo(x,y+h,x,y,r); context.arcTo(x,y,x+w,y,r); context.closePath(); context.fill();
        };
        var draw = function(image) {
            if (closed) return;
            if (data.fullCardImage && image) {
                context.clearRect(0,0,1080,1350); context.drawImage(image,0,0,1080,1350);
            } else if (data.fullCardImage) {
                status.textContent=text('Не удалось подготовить картинку. Ссылка доступна.', 'Could not prepare the image. The link is available.');
                return;
            } else {
            context.fillStyle=paper; context.fillRect(0,0,1080,1350);
            roundRect(56,52,968,110,28,'#41266f'); context.fillStyle='#fff'; context.textAlign='center';
            context.font='900 55px Arial,sans-serif'; context.fillText(text('МОЙ ВОДОВОРОТ','MY VODOVOROT'),540,126);
            context.fillStyle='#f7e9ce'; context.beginPath(); context.ellipse(540,495,335,245,0,0,Math.PI*2); context.fill();
            if (image) context.drawImage(image,220,182,640,640);
            context.fillStyle='#30223e'; context.font='bold 37px Arial,sans-serif'; context.fillText(String(data.skin || '').slice(0,36),540,850);
            context.fillStyle='#41266f'; context.font='900 150px Arial,sans-serif'; context.fillText(String(data.percent || '0.0%'),540,1030);
            context.fillStyle='#504560'; context.font='30px Arial,sans-serif';
            context.fillText(String(data.map || '') + ' · ' + String(data.mode || ''),540,1090,960);
            if (data.rank > 0) {
                roundRect(150,1130,780,85,22,'#f4bd43'); context.fillStyle='#30223e'; context.font='bold 39px Arial,sans-serif';
                context.fillText(text('МЕСТО В РЕЙТИНГЕ: ','GLOBAL RANK: ') + data.rank,540,1187,740);
            } else {
                context.fillStyle='#30223e'; context.font='bold 36px Arial,sans-serif';
                context.fillText(text('Расту с каждой игрой','Growing with every game'),540,1187);
            }
            context.fillStyle='#504560'; context.font='27px Arial,sans-serif'; context.fillText(url,540,1290,960);
            }
            canvas.toBlob(function(blob) {
                if (!blob || !overlay.parentNode) { status.textContent = text('Не удалось подготовить картинку. Ссылка доступна.', 'Could not prepare the image. The link is available.'); return; }
                objectURL=URL.createObjectURL(blob);
                if (typeof File !== 'undefined') shareFile = new File([blob],'vodovorot-achievement.png',{type:'image/png'});
                shareButton.disabled=false; saveButton.disabled=false;
                status.textContent=text('Достижение готово — поделитесь им!', 'Your achievement is ready to share!');
            },'image/png');
        };
        if (portrait) {
            var image = new Image(); image.onload=function() {draw(image);}; image.onerror=function() {draw(null);};
            image.src='data:image/png;base64,' + portrait;
        } else draw(null);
    }
});
