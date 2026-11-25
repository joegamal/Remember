<!DOCTYPE html>
<html lang="{{ str_replace('_', '-', app()->getLocale()) }}">
    <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{{ config('app.name', 'Laravel') }}</title>
        <link rel="stylesheet" href="{{ asset('css/style.css') }}">
    </head>
    <body>
        <head><nav>
            <div class="header-nav">
                <a href="#">home</a>
                <a href="{{route('about')}}">about</a>
                <a href="#">github</a>
            </div>
        </nav></head>



    <script src="{{asset('js/main.js')}}"></script>
    </body>
</html>
