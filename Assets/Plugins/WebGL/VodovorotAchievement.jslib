mergeInto(LibraryManager.library, {
    VodovorotAchievementOpen: function(jsonPointer, portraitPointer) {
        var data;
        try { data = JSON.parse(UTF8ToString(jsonPointer)); } catch (_) { return; }
        var portrait = UTF8ToString(portraitPointer);
        var previous = document.getElementById('vodovorot-achievement');
        if (previous && previous.vodoClose) previous.vodoClose();
        var ru = data.ru === true;
        var text = function(russian, english) { return ru ? russian : english; };
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
        overlay.style.cssText = 'position:fixed;inset:0;z-index:2147483646;background:rgba(18,12,32,.94);color:#fff;display:flex;flex-direction:column;align-items:center;justify-content:flex-start;box-sizing:border-box;padding:max(12px,env(safe-area-inset-top)) 16px max(12px,env(safe-area-inset-bottom));gap:10px;overflow:auto;font:16px Arial,sans-serif;user-select:auto;overscroll-behavior:contain;';
        var header = document.createElement('div');
        header.style.cssText = 'display:flex;align-items:center;justify-content:space-between;width:min(100%,560px);flex-shrink:0;';
        var heading = document.createElement('strong'); heading.textContent = text('Моё достижение', 'My achievement');
        heading.style.cssText = 'font-size:20px;'; header.appendChild(heading);
        var buttonStyle = 'border:0;border-radius:14px;min-height:48px;padding:10px 18px;font:bold 16px Arial,sans-serif;cursor:pointer;background:#f5bf47;color:#251b39;touch-action:manipulation;';
        var makeButton = function(label, fn) {
            var button = document.createElement('button'); button.type = 'button'; button.textContent = label;
            button.style.cssText = buttonStyle; button.addEventListener('click', fn); return button;
        };
        var objectURL = null;
        var activeBefore = document.activeElement;
        var keyHandler;
        var close = function() {
            document.removeEventListener('keydown', keyHandler);
            if (overlay.parentNode) overlay.parentNode.removeChild(overlay);
            if (objectURL) { var revokedURL = objectURL; setTimeout(function() { URL.revokeObjectURL(revokedURL); }, 1500); }
            if (activeBefore && activeBefore.focus) activeBefore.focus({preventScroll:true});
        };
        overlay.vodoClose = close;
        var closeButton = makeButton(text('Закрыть', 'Close'), close);
        closeButton.style.background = '#392950'; closeButton.style.color = '#fff'; header.appendChild(closeButton);
        overlay.appendChild(header);
        var canvas = document.createElement('canvas'); canvas.width = 1080; canvas.height = 1350;
        canvas.setAttribute('aria-label', text('Карточка с результатом игры', 'Game result card'));
        canvas.style.cssText = 'display:block;max-width:min(100%,560px);max-height:calc(100dvh - 230px);width:auto;height:auto;object-fit:contain;border-radius:20px;flex-shrink:1;min-height:180px;';
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
        var shareButton = makeButton(text('Отправить другу', 'Share'), function() {
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
                    status.textContent = text('Передано в меню отправки.', 'Passed to the share menu.');
                }).catch(function(error) {
                    status.textContent = error && error.name === 'AbortError'
                        ? text('Отправка отменена.', 'Sharing cancelled.')
                        : text('Отправка здесь недоступна. Сохраните карточку и скопируйте ссылку.', 'Sharing is unavailable here. Save the card and copy the link.');
                });
            } catch (_) { status.textContent = text('Сохраните карточку и скопируйте ссылку.', 'Save the card and copy the link.'); }
        });
        shareButton.disabled = true;
        var saveButton = makeButton(text('Сохранить картинку', 'Save image'), function() {
            if (!objectURL) return;
            var link = document.createElement('a'); link.href = objectURL; link.download = 'vodovorot-achievement.png';
            document.body.appendChild(link); link.click(); link.remove();
            status.textContent = text('Картинка передана браузеру для сохранения.', 'Image passed to your browser for saving.');
        }); saveButton.disabled = true;
        var copyButton = makeButton(text('Скопировать ссылку', 'Copy link'), function() {
            var manual = function() {
                var input = manualInput || document.createElement('input'); manualInput = input;
                input.value = url; input.readOnly = true;
                input.setAttribute('aria-label', text('Ссылка на игру', 'Game link'));
                input.style.cssText = 'width:min(90%,560px);min-height:44px;font:16px Arial,sans-serif;';
                if (!input.parentNode) overlay.appendChild(input);
                input.focus(); input.select();
                var copied = false;
                try { copied = document.execCommand('copy'); } catch (_) { }
                if (copied) { input.remove(); status.textContent = text('Ссылка скопирована.', 'Link copied.'); }
                else status.textContent = text('Скопируйте выделенную ссылку вручную.', 'Copy the selected link manually.');
            };
            if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(url).then(function() {
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
        document.addEventListener('keydown', keyHandler);
        document.body.appendChild(overlay); closeButton.focus();
        var context = canvas.getContext('2d');
        var roundRect = function(x,y,w,h,r,color) {
            context.fillStyle=color; context.beginPath(); context.moveTo(x+r,y); context.arcTo(x+w,y,x+w,y+h,r);
            context.arcTo(x+w,y+h,x,y+h,r); context.arcTo(x,y+h,x,y,r); context.arcTo(x,y,x+w,y,r); context.closePath(); context.fill();
        };
        var draw = function(image) {
            context.fillStyle='#fffaeb'; context.fillRect(0,0,1080,1350);
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
