## Server-Sent Events in F#

publishing

```sh
curl http://localhost:5000/sse -d "sigurd var her"
```

subscribing

```sh
curl --http2 -N http://localhost:5000/sse
```
