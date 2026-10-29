import React, {useEffect, useRef, useState} from 'react';
import {View, Text, Pressable} from 'react-native';
import UnityView from '@azesmway/react-native-unity';

export default function App() {
  const unityRef = useRef<UnityView>(null);
  const messageCounter = useRef(0);
  const [last, setLast] = useState('nothing yet');
  const [showAR, setShowAR] = useState(true);

  // Temporary contract controls for testing the modular AR session on-device.
  const sendToUnity = (type: string, payload: object = {}) => {
    messageCounter.current += 1;
    const message = {
      v: 1,
      id: `s-${Date.now()}-${messageCounter.current}`,
      type,
      payload,
    };
    unityRef.current?.postMessage(
      'LociBridge',
      'OnShellMessage',
      JSON.stringify(message),
    );
  };

  // Pause Unity when its screen is covered, resume when it comes back.
  useEffect(() => {
    if (showAR) {
      unityRef.current?.resumeUnity();
      unityRef.current?.windowFocusChanged(true);
    } else {
      unityRef.current?.pauseUnity(true);
    }
  }, [showAR]);

  const btn = {marginTop: 8, padding: 14, backgroundColor: '#5B4BFF'};
  const btnText = {color: '#fff', fontWeight: '600' as const};

  return (
    <View style={{flex: 1, backgroundColor: '#F4F5F9'}}>
      <UnityView
        ref={unityRef}
        androidKeepPlayerMounted={true}
        style={{flex: 1}}
        onUnityMessage={e => setLast(e.nativeEvent.message)}
      />
      {!showAR && (
        <View
          style={{
            position: 'absolute', top: 0, left: 0, right: 0, bottom: 0,
            backgroundColor: '#F4F5F9', alignItems: 'center', justifyContent: 'center',
          }}>
          <Text style={{fontSize: 22}}>Another screen (AR paused)</Text>
        </View>
      )}
      <View style={{position: 'absolute', top: 48, left: 16, right: 16}}>
        <Text style={{color: '#fff', backgroundColor: '#000a', padding: 8}}>
          From Unity: {last}
        </Text>
        <Text style={{color: '#fff', backgroundColor: '#000a', padding: 8, marginTop: 8}}>
          Temporary AR test controls
        </Text>
        <Pressable
          onPress={() => sendToUnity('diag.ping', {nonce: `android-${Date.now()}`})}
          style={btn}>
          <Text style={btnText}>Send diag.ping</Text>
        </Pressable>
        <Pressable onPress={() => sendToUnity('session.start')} style={btn}>
          <Text style={btnText}>session.start</Text>
        </Pressable>
        <Pressable onPress={() => sendToUnity('session.stop')} style={btn}>
          <Text style={btnText}>session.stop</Text>
        </Pressable>
        <Pressable onPress={() => sendToUnity('placement.confirm')} style={btn}>
          <Text style={btnText}>placement.confirm (Place here)</Text>
        </Pressable>
        <Pressable onPress={() => sendToUnity('placement.reset')} style={btn}>
          <Text style={btnText}>placement.reset</Text>
        </Pressable>
        <Pressable
          onPress={() => sendToUnity('topic.open', {topicId: 'array_list'})}
          style={btn}>
          <Text style={btnText}>Open Array/List</Text>
        </Pressable>
        <Pressable
          onPress={() =>
            sendToUnity('topic.action', {
              topicId: 'array_list',
              action: 'Insert',
              value: '40',
            })
          }
          style={btn}>
          <Text style={btnText}>Insert 40</Text>
        </Pressable>
        <Pressable onPress={() => setShowAR(s => !s)} style={btn}>
          <Text style={btnText}>{showAR ? 'Leave AR screen' : 'Back to AR screen'}</Text>
        </Pressable>
      </View>
    </View>
  );
}
