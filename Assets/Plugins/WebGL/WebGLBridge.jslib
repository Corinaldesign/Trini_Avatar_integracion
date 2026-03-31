mergeInto(LibraryManager.library, {

    /**
     * Envia un evento desde Unity a React.
     * @param {string} eventNamePtr - Puntero al nombre del evento
     * @param {string} eventDataPtr - Puntero a los datos JSON del evento
     */
    SendUnityEvent: function(eventNamePtr, eventDataPtr) {
        var eventName = UTF8ToString(eventNamePtr);
        var eventData = UTF8ToString(eventDataPtr);

        // Crear evento personalizado
        var event = new CustomEvent('unityEvent', {
            detail: {
                name: eventName,
                data: eventData
            }
        });

        // Disparar en window para que React pueda escucharlo
        window.dispatchEvent(event);

        // Log para debugging
        console.log('[Unity->React] Event:', eventName, 'Data:', eventData);
    }

});
