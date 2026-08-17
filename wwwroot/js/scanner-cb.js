/*
 * scanner-cb.js — lecture de codes-barres par la caméra du téléphone.
 *
 * S'appuie sur l'API BarcodeDetector (native dans Chrome/Edge Android et Chrome bureau).
 * Aucune librairie externe : si l'API manque (Safari iOS notamment), on le dit clairement
 * à l'utilisateur au lieu d'échouer en silence.
 *
 * Utilisation :
 *   ScannerCB.ouvrir({
 *     titre: 'Scanner un article',
 *     continu: true,                       // rester ouvert et enchaîner les scans
 *     onCode: async (code) => 'texte à afficher sous le viseur'
 *   });
 */
(function (global) {
    'use strict';

    const FORMATS_VOULUS = ['ean_13', 'ean_8', 'upc_a', 'upc_e', 'code_128', 'code_39', 'itf', 'codabar', 'qr_code'];
    const DELAI_ANTI_DOUBLON = 1500; // ms pendant lesquels le même code est ignoré

    let overlay = null, video = null, flux = null, boucle = null;
    let dernierCode = '', dernierTemps = 0, occupe = false;
    let audioCtx = null;

    const dispo = () => typeof global.BarcodeDetector !== 'undefined';
    const securise = () => global.isSecureContext === true;

    function bip(ok) {
        try {
            audioCtx = audioCtx || new (global.AudioContext || global.webkitAudioContext)();
            const o = audioCtx.createOscillator(), g = audioCtx.createGain();
            o.type = 'square';
            o.frequency.value = ok ? 1180 : 320;
            g.gain.value = 0.05;
            o.connect(g); g.connect(audioCtx.destination);
            o.start();
            o.stop(audioCtx.currentTime + (ok ? 0.07 : 0.22));
        } catch { /* le son n'est qu'un confort */ }
        if (navigator.vibrate) navigator.vibrate(ok ? 40 : [60, 50, 60]);
    }

    function statut(texte, type) {
        if (!overlay) return;
        const el = overlay.querySelector('.scan-statut');
        el.textContent = texte || '';
        el.className = 'scan-statut' + (type ? ' ' + type : '');
    }

    function construireOverlay(titre) {
        const o = document.createElement('div');
        o.className = 'scan-overlay';
        o.innerHTML = `
            <div class="scan-barre">
                <span class="scan-titre"></span>
                <div class="scan-actions">
                    <button type="button" class="scan-btn scan-torche" hidden>🔦 Lampe</button>
                    <button type="button" class="scan-btn scan-fermer">✕ Fermer</button>
                </div>
            </div>
            <div class="scan-vue">
                <video class="scan-video" playsinline muted autoplay></video>
                <div class="scan-viseur"><span class="scan-laser"></span></div>
            </div>
            <div class="scan-pied">
                <div class="scan-statut">Visez le code-barres…</div>
                <form class="scan-manuel">
                    <input type="text" inputmode="numeric" autocomplete="off" placeholder="…ou tapez le code à la main" />
                    <button type="submit" class="scan-btn scan-valider">Ajouter</button>
                </form>
            </div>`;
        o.querySelector('.scan-titre').textContent = titre || 'Scanner';
        return o;
    }

    async function traiter(code, onCode) {
        const maintenant = Date.now();
        if (code === dernierCode && maintenant - dernierTemps < DELAI_ANTI_DOUBLON) return;
        dernierCode = code; dernierTemps = maintenant;

        occupe = true;
        try {
            const msg = await onCode(code);
            const echec = typeof msg === 'string' && msg.startsWith('!');
            bip(!echec);
            statut(echec ? msg.slice(1) : (msg || code), echec ? 'ko' : 'ok');
        } catch (e) {
            bip(false);
            statut('Erreur : ' + e.message, 'ko');
        } finally {
            occupe = false;
        }
    }

    async function demarrerCamera(opts) {
        flux = await navigator.mediaDevices.getUserMedia({
            video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } },
            audio: false
        });
        video.srcObject = flux;
        await video.play();

        // Lampe torche, quand le téléphone l'expose.
        const piste = flux.getVideoTracks()[0];
        const caps = piste.getCapabilities ? piste.getCapabilities() : {};
        if (caps.torch) {
            const btn = overlay.querySelector('.scan-torche');
            btn.hidden = false;
            let allumee = false;
            btn.addEventListener('click', async () => {
                allumee = !allumee;
                try {
                    await piste.applyConstraints({ advanced: [{ torch: allumee }] });
                    btn.classList.toggle('actif', allumee);
                } catch { statut('Lampe non disponible.', 'ko'); }
            });
        }

        const formatsOk = await global.BarcodeDetector.getSupportedFormats();
        const formats = FORMATS_VOULUS.filter(f => formatsOk.includes(f));
        const detecteur = new global.BarcodeDetector({ formats: formats.length ? formats : undefined });

        boucle = setInterval(async () => {
            if (occupe || !video || video.readyState < 2) return;
            let codes;
            try { codes = await detecteur.detect(video); }
            catch { return; }
            if (!codes || !codes.length) return;

            await traiter(codes[0].rawValue, opts.onCode);
            if (!opts.continu) fermer();
        }, 180);
    }

    async function ouvrir(opts) {
        opts = opts || {};
        if (overlay) return;

        // Caméra impossible ici : on ouvre quand même, en saisie manuelle, plutôt que de bloquer.
        // Étiquette déchirée, appareil sans caméra, iPhone sans BarcodeDetector : la caisse doit continuer.
        let raison = null;
        if (!securise())
            raison = "Caméra indisponible : le site doit être servi en HTTPS (ou depuis localhost). "
                   + "Saisissez le code à la main en attendant.";
        else if (!dispo())
            raison = "Ce navigateur ne décode pas les codes-barres (Chrome ou Edge sur Android le font). "
                   + "Saisissez le code à la main.";

        overlay = construireOverlay(opts.titre);
        document.body.appendChild(overlay);
        document.body.classList.add('scan-ouvert');
        video = overlay.querySelector('.scan-video');

        overlay.querySelector('.scan-fermer').addEventListener('click', fermer);
        overlay.addEventListener('keydown', e => { if (e.key === 'Escape') fermer(); });

        // Saisie manuelle de secours : code illisible, étiquette abîmée, article sans code-barres.
        overlay.querySelector('.scan-manuel').addEventListener('submit', async e => {
            e.preventDefault();
            const inp = e.target.querySelector('input');
            const v = inp.value.trim();
            if (!v) return;
            dernierCode = ''; // une saisie manuelle n'est jamais un doublon
            await traiter(v, opts.onCode);
            inp.value = '';
            if (!opts.continu) fermer();
        });

        const champ = overlay.querySelector('.scan-manuel input');

        if (raison) {
            overlay.querySelector('.scan-vue').hidden = true;
            statut(raison, 'ko');
            champ.focus();
            return;
        }

        try {
            await demarrerCamera(opts);
        } catch (e) {
            overlay.querySelector('.scan-viseur').hidden = true;
            statut(e.name === 'NotAllowedError'
                ? "Accès caméra refusé. Autorisez la caméra pour ce site, ou tapez le code ci-dessous."
                : "Caméra inaccessible (" + e.message + "). Tapez le code ci-dessous.", 'ko');
            champ.focus();
        }
    }

    function fermer() {
        if (boucle) { clearInterval(boucle); boucle = null; }
        if (flux) { flux.getTracks().forEach(t => t.stop()); flux = null; }
        if (overlay) { overlay.remove(); overlay = null; }
        document.body.classList.remove('scan-ouvert');
        video = null; occupe = false; dernierCode = '';
    }

    global.addEventListener('pagehide', fermer);

    global.ScannerCB = { ouvrir, fermer, disponible: dispo, contexteSecurise: securise, statut, bip };
})(window);
