const http = require('http');
const fs = require('fs');
const path = require('path');
const url = require('url');

const hostname = '127.0.0.1';
const port = 3000;
const dictionaryFile = path.join(__dirname, 'dictionary.txt');

const server = http.createServer((req, res) => {
    const parsedUrl = url.parse(req.url, true);
    const { pathname } = parsedUrl;

    if (pathname === '/' && req.method === 'GET') {
        fs.readFile(path.join(__dirname, 'index.html'), 'utf8', (err, data) => {
            if (err) {
                res.statusCode = 500;
                res.end('Internal Server Error');
                return;
            }
            res.statusCode = 200;
            res.setHeader('Content-Type', 'text/html');
            res.end(data);
        });
    } else if (pathname === '/dictionary' && req.method === 'GET') {
        fs.readFile(dictionaryFile, 'utf8', (err, data) => {
            if (err) {
                res.statusCode = 500;
                res.end('Internal Server Error');
                return;
            }
            res.statusCode = 200;
            res.setHeader('Content-Type', 'text/plain');
            res.end(data);
        });
    } else if (pathname === '/dictionary' && req.method === 'POST') {
        let body = '';
        req.on('data', chunk => {
            body += chunk.toString();
        });
        req.on('end', () => {
            fs.writeFile(dictionaryFile, body, 'utf8', err => {
                if (err) {
                    res.statusCode = 500;
                    res.end('Internal Server Error');
                    return;
                }
                res.statusCode = 200;
                res.end('Dictionary saved');
            });
        });
    } else {
        res.statusCode = 404;
        res.end('Not Found');
    }
});

server.listen(port, hostname, () => {
    console.log(`Server running at http://${hostname}:${port}/`);
});
